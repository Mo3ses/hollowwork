using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Threading;
using Hollowwork.Automation;

namespace Hollowwork.App.ViewModels;

public sealed class LiveViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly AutomationProfile? _profile;
    private readonly string _profileError;
    private AutoFlaskEngine? _engine;
    private AreaWatcher? _area;
    private bool _hpEnabled = true;
    private bool _esEnabled;
    private bool _mpEnabled = true;
    private double _hpThreshold = 40;
    private double _esThreshold = 50;
    private double _mpThreshold = 30;
    private string _hpKey = "1";
    private string _esKey = "3";
    private string _mpKey = "2";
    private string _cooldownText = "1000";
    private bool _requireFocus = true;
    private bool _pauseInTown = true;
    private bool _pauseInHideout = true;
    private string _clientLogPath = string.Empty;
    private string _areaText = "AREA · not watched";
    private string _statusText = "STOPPED";
    private string _lastAction = "Select START to attach to PoE1.";
    private string _hpText = "HP —";
    private string _esText = "ES —";
    private string _mpText = "MP —";
    private double _hpPercent;
    private double _esPercent;
    private double _mpPercent;
    private bool _isRunning;

    public LiveViewModel()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "automation", "poe1.json");
        try
        {
            _profile = AutomationProfile.Load(path);
            _profileError = string.Empty;
        }
        catch (Exception exception)
        {
            _profileError = exception.Message;
            StatusText = $"PROFILE ERROR · {exception.Message}";
        }

        UpdateAreaHint();
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(150), DispatcherPriority.Background, (_, _) => Refresh());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool HpEnabled
    {
        get => _hpEnabled;
        set => Set(ref _hpEnabled, value);
    }

    public bool EsEnabled
    {
        get => _esEnabled;
        set => Set(ref _esEnabled, value);
    }

    public bool MpEnabled
    {
        get => _mpEnabled;
        set => Set(ref _mpEnabled, value);
    }

    public double HpThreshold
    {
        get => _hpThreshold;
        set => Set(ref _hpThreshold, value);
    }

    public double EsThreshold
    {
        get => _esThreshold;
        set => Set(ref _esThreshold, value);
    }

    public double MpThreshold
    {
        get => _mpThreshold;
        set => Set(ref _mpThreshold, value);
    }

    public string HpKey
    {
        get => _hpKey;
        set => Set(ref _hpKey, value);
    }

    public string EsKey
    {
        get => _esKey;
        set => Set(ref _esKey, value);
    }

    public string MpKey
    {
        get => _mpKey;
        set => Set(ref _mpKey, value);
    }

    public string CooldownText
    {
        get => _cooldownText;
        set => Set(ref _cooldownText, value);
    }

    public bool RequireFocus
    {
        get => _requireFocus;
        set => Set(ref _requireFocus, value);
    }

    public bool PauseInTown
    {
        get => _pauseInTown;
        set => Set(ref _pauseInTown, value);
    }

    public bool PauseInHideout
    {
        get => _pauseInHideout;
        set => Set(ref _pauseInHideout, value);
    }

    /// <summary>Full path to <c>Client.txt</c>; empty means auto-discovery.</summary>
    public string ClientLogPath
    {
        get => _clientLogPath;
        set
        {
            if (Set(ref _clientLogPath, value) && !IsRunning)
            {
                UpdateAreaHint();
            }
        }
    }

    public string AreaText
    {
        get => _areaText;
        private set => Set(ref _areaText, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => Set(ref _statusText, value);
    }

    public string LastAction
    {
        get => _lastAction;
        private set => Set(ref _lastAction, value);
    }

    public string HpText
    {
        get => _hpText;
        private set => Set(ref _hpText, value);
    }

    public string EsText
    {
        get => _esText;
        private set => Set(ref _esText, value);
    }

    public string MpText
    {
        get => _mpText;
        private set => Set(ref _mpText, value);
    }

    public double HpPercent
    {
        get => _hpPercent;
        private set => Set(ref _hpPercent, value);
    }

    public double EsPercent
    {
        get => _esPercent;
        private set => Set(ref _esPercent, value);
    }

    public double MpPercent
    {
        get => _mpPercent;
        private set => Set(ref _mpPercent, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (Set(ref _isRunning, value))
            {
                OnPropertyChanged(nameof(StartLabel));
            }
        }
    }

    public string StartLabel => IsRunning ? "STOP" : "START";

    public void Toggle()
    {
        if (IsRunning)
        {
            Stop();
            return;
        }

        Start();
    }

    public void Start()
    {
        if (_profile is null)
        {
            StatusText = $"PROFILE ERROR · {_profileError}";
            return;
        }

        if (!Poe1Reader.TryOpen(_profile, out var reader, out var error))
        {
            StatusText = $"ATTACH FAILED · {error}";
            LastAction = "Start PoE1 through Steam/Proton, then try again.";
            return;
        }

        try
        {
            var input = new X11KeySink();
            var focus = new X11FocusProbe();
            _area = new AreaWatcher(string.IsNullOrWhiteSpace(ClientLogPath) ? null : ClientLogPath.Trim());
            _area.Poll();
            _engine = new AutoFlaskEngine(reader!, input, focus, _profile, _area);
            _engine.Start(Options());
            IsRunning = true;
            StatusText = $"RUNNING · PID {_engine.ProcessId}";
            LastAction = "Attached. Flask keys are sent only while the game is focused.";
            _timer.Start();
        }
        catch (Exception exception)
        {
            if (_engine is not null)
            {
                _engine.Dispose();
                _engine = null;
            }
            else
            {
                reader?.Dispose();
            }

            StatusText = $"START FAILED · {exception.Message}";
            LastAction = "X11 input unavailable. Check DISPLAY.";
        }
    }

    public void Stop()
    {
        _timer.Stop();
        _engine?.Stop();
        _engine?.Dispose();
        _engine = null;
        _area?.Dispose();
        _area = null;
        IsRunning = false;
        AreaText = "AREA · not watched";
        StatusText = "STOPPED";
        LastAction = "No keys are being sent.";
    }

    public void Dispose()
    {
        _timer.Stop();
        _engine?.Dispose();
        _engine = null;
        _area?.Dispose();
        _area = null;
    }

    private AutoFlaskOptions Options()
    {
        var cooldown = int.TryParse(CooldownText, out var parsed) ? Math.Clamp(parsed, 100, 60_000) : 1000;
        return new AutoFlaskOptions(
            HpEnabled,
            Math.Clamp(HpThreshold / 100d, 0.01, 0.99),
            EsEnabled,
            Math.Clamp(EsThreshold / 100d, 0.01, 0.99),
            MpEnabled,
            Math.Clamp(MpThreshold / 100d, 0.01, 0.99),
            HpKey,
            EsKey,
            MpKey,
            cooldown,
            RequireFocus,
            PauseInTown,
            PauseInHideout);
    }

    private void UpdateAreaHint()
    {
        var path = string.IsNullOrWhiteSpace(_clientLogPath) ? AreaWatcher.Discover() : _clientLogPath.Trim();
        AreaText = path is null ? "AREA OFF · Client.txt not found" : $"AREA · {path}";
    }

    private void Refresh()
    {
        if (_engine is null)
        {
            return;
        }

        StatusText = _engine.StatusText;
        AreaText = _area switch
        {
            { IsAvailable: true } => $"AREA · {_engine.AreaLabel ?? "—"}",
            { Error: { } error } => $"AREA OFF · {error}",
            _ => "AREA · not watched",
        };

        var action = _engine.LastAction;
        if (!string.IsNullOrWhiteSpace(action))
        {
            LastAction = action;
        }

        var vitals = _engine.Vitals;
        if (vitals is null)
        {
            HpText = "HP —";
            EsText = "ES —";
            MpText = "MP —";
            HpPercent = EsPercent = MpPercent = 0;
            return;
        }

        var value = vitals.Value;
        HpText = $"HP {value.Hp} / {value.MaxHp} ({value.HpPercent:0}%)";
        EsText = $"ES {value.Es} / {value.MaxEs} ({value.EsPercent:0}%)";
        MpText = $"MP {value.Mp} / {value.MaxMp} ({value.MpPercent:0}%)";
        HpPercent = Math.Clamp(value.HpPercent, 0, 100);
        EsPercent = Math.Clamp(value.EsPercent, 0, 100);
        MpPercent = Math.Clamp(value.MpPercent, 0, 100);
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? property = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(property);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? property = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}
