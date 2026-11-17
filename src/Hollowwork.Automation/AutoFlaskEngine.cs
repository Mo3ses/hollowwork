namespace Hollowwork.Automation;

public sealed record AutoFlaskOptions(
    bool HpEnabled = true,
    double HpThreshold = 0.40,
    bool EsEnabled = false,
    double EsThreshold = 0.50,
    bool MpEnabled = true,
    double MpThreshold = 0.30,
    string HpKey = "1",
    string EsKey = "3",
    string MpKey = "2",
    int CooldownMs = 1000,
    bool RequireFocus = true,
    bool PauseInTown = true,
    bool PauseInHideout = true);

public sealed class AutoFlaskEngine : IDisposable
{
    private readonly Poe1Reader _reader;
    private readonly IKeySink _input;
    private readonly IFocusProbe _focus;
    private readonly AutomationProfile _profile;
    private readonly IAreaProbe? _area;
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private AutoFlaskOptions _options = new();
    private FlaskChannel _hpChannel = new(null);
    private FlaskChannel _esChannel = new(null);
    private FlaskChannel _mpChannel = new(null);
    private PlayerVitals? _vitals;
    private string _status = "STOPPED";
    private string _lastAction = string.Empty;
    private volatile bool _running;

    public AutoFlaskEngine(
        Poe1Reader reader,
        IKeySink input,
        IFocusProbe focus,
        AutomationProfile profile,
        IAreaProbe? area = null)
    {
        _reader = reader;
        _input = input;
        _focus = focus;
        _profile = profile;
        _area = area;
    }

    /// <summary>Zone the automation is paused in, when it is paused by area.</summary>
    public string? AreaLabel => _area is { IsAvailable: true } area
        ? area.IsTransitioning ? "LOADING" : area.Area
        : null;

    public int ProcessId => _reader.ProcessId;

    public bool IsAttached => _reader.IsAttached;

    public bool IsRunning => _running;

    public PlayerVitals? Vitals => _vitals;

    public string StatusText => Volatile.Read(ref _status);

    public string LastAction => Volatile.Read(ref _lastAction);

    public void Start(AutoFlaskOptions options)
    {
        if (_running)
        {
            return;
        }

        _options = options;
        _hpChannel = new FlaskChannel(options.HpKey);
        _esChannel = new FlaskChannel(options.EsKey);
        _mpChannel = new FlaskChannel(options.MpKey);
        _cts = new CancellationTokenSource();
        _running = true;
        _loop = Task.Run(() => LoopAsync(_cts.Token));
    }

    public void Stop()
    {
        if (!_running)
        {
            return;
        }

        _cts?.Cancel();
        _running = false;
        SetStatus("STOPPED");
    }

    private bool ShouldPause(IAreaProbe area)
    {
        if (area.IsTransitioning)
        {
            return true;
        }

        return area.Kind switch
        {
            AreaKind.Town => _options.PauseInTown,
            AreaKind.Hideout => _options.PauseInHideout,
            _ => false,
        };
    }

    public static bool ShouldPress(
        int current,
        int max,
        double threshold,
        DateTime now,
        DateTime lastPress,
        int cooldownMs)
    {
        if (max <= 0 || current <= 0 || threshold <= 0 || threshold >= 1)
        {
            return false;
        }

        var ratio = (double)current / max;
        return ratio < threshold &&
               (now - lastPress).TotalMilliseconds >= Math.Max(0, cooldownMs);
    }

    public void Dispose()
    {
        Stop();
        try
        {
            _loop?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
        }

        _cts?.Dispose();
        _input.Dispose();
        _focus.Dispose();
        _reader.Dispose();
    }

    private async Task LoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var vitals = _reader.ReadVitals();
                if (vitals is null)
                {
                    if (!_reader.IsProcessAlive)
                    {
                        SetStatus("GAME CLOSED · press START after reopening");
                        _running = false;
                        _cts?.Cancel();
                        break;
                    }

                    if (!_reader.TryAttach(out var error))
                    {
                        SetStatus($"ATTACH FAILED · {error}");
                        await Task.Delay(1000, cancellationToken);
                        continue;
                    }

                    vitals = _reader.ReadVitals();
                }

                if (vitals is not { IsSane: true } value)
                {
                    _vitals = null;
                    SetStatus("WAITING FOR PLAYER");
                    await Task.Delay(500, cancellationToken);
                    continue;
                }

                _vitals = value;

                if (_options.RequireFocus && !_focus.IsGameFocused(_profile.WindowTitle, _profile.WindowClass))
                {
                    SetStatus("RUNNING · GAME NOT FOCUSED");
                    await Task.Delay(250, cancellationToken);
                    continue;
                }

                _area?.Poll();
                if (_area is { IsAvailable: true } area && ShouldPause(area))
                {
                    SetStatus(area.IsTransitioning ? "PAUSED · LOADING" : $"PAUSED · {area.Kind.ToString().ToUpperInvariant()} · {area.Area}");
                    await Task.Delay(250, cancellationToken);
                    continue;
                }

                var now = DateTime.UtcNow;
                if (_options.HpEnabled &&
                    ShouldPress(value.Hp, value.MaxHp, _options.HpThreshold, now, _hpChannel.LastPress, _options.CooldownMs) &&
                    _hpChannel.Press(now, _options.CooldownMs) is { } hpKey)
                {
                    _input.Press(hpKey);
                    SetAction($"HP FLASK {hpKey} · {value.Hp}/{value.MaxHp}");
                }

                if (_options.EsEnabled &&
                    ShouldPress(value.Es, value.MaxEs, _options.EsThreshold, now, _esChannel.LastPress, _options.CooldownMs) &&
                    _esChannel.Press(now, _options.CooldownMs) is { } esKey)
                {
                    _input.Press(esKey);
                    SetAction($"ES FLASK {esKey} · {value.Es}/{value.MaxEs}");
                }

                if (_options.MpEnabled &&
                    ShouldPress(value.Mp, value.MaxMp, _options.MpThreshold, now, _mpChannel.LastPress, _options.CooldownMs) &&
                    _mpChannel.Press(now, _options.CooldownMs) is { } mpKey)
                {
                    _input.Press(mpKey);
                    SetAction($"MP FLASK {mpKey} · {value.Mp}/{value.MaxMp}");
                }

                SetStatus($"RUNNING · HP {value.Hp}/{value.MaxHp} · ES {value.Es}/{value.MaxEs} · MP {value.Mp}/{value.MaxMp}");
                await Task.Delay(50, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                SetStatus($"ERROR · {exception.Message}");
                await Task.Delay(1000, cancellationToken);
            }
        }
    }

    private void SetStatus(string status) => Volatile.Write(ref _status, status);

    private void SetAction(string action) => Volatile.Write(ref _lastAction, action);
}
