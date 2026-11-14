using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using Hollowwork.Core.Market;

namespace Hollowwork.App.ViewModels;

public sealed class FlipViewModel : INotifyPropertyChanged, IDisposable
{
    private const int HourCount = 6;
    private const int MinVolume = 100;
    private const double CapitalChaos = 500;

    private readonly CurrencyExchangeClient _client;
    private readonly List<CurrencyExchangeHour> _hours = [];
    private CancellationTokenSource? _cts;
    private int _version;
    private bool _active;
    private bool _loading;
    private bool _loadedOnce;
    private string _loadedGame = string.Empty;
    private string _game = "poe1";
    private string? _selectedLeague;
    private string _statusText = "READ-ONLY · OPEN THE TAB TO LOAD";
    private string _detailText = "Select a pair to see the estimate.";
    private FlipRowViewModel? _selectedOpportunity;

    public FlipViewModel()
    {
        _client = new CurrencyExchangeClient(
            Path.Combine(AppPaths.DataRoot, "market"));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> Leagues { get; } = [];

    public ObservableCollection<FlipRowViewModel> Opportunities { get; } = [];

    public string FilterText => $"MIN {MinVolume} c/h · {CapitalChaos:0}c CAPITAL";

    public string Disclaimer =>
        "READ-ONLY · hourly aggregate, not a live order book. Confirm prices at Faustus before trading.";

    public bool IsLoading
    {
        get => _loading;
        private set
        {
            if (_loading == value)
            {
                return;
            }

            _loading = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanRefresh));
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    public bool CanRefresh => !_loading;

    public bool HasOpportunities => Opportunities.Count > 0;

    public bool IsEmpty => !_loading && Opportunities.Count == 0;

    public string StatusText
    {
        get => _statusText;
        private set => Set(ref _statusText, value);
    }

    public string DetailText
    {
        get => _detailText;
        private set => Set(ref _detailText, value);
    }

    public string? SelectedLeague
    {
        get => _selectedLeague;
        set
        {
            if (_selectedLeague == value)
            {
                return;
            }

            _selectedLeague = value;
            OnPropertyChanged();
            Analyze();
        }
    }

    public FlipRowViewModel? SelectedOpportunity
    {
        get => _selectedOpportunity;
        set
        {
            if (ReferenceEquals(_selectedOpportunity, value))
            {
                return;
            }

            _selectedOpportunity = value;
            OnPropertyChanged();
            DetailText = value?.DetailText ?? "Select a pair to see the estimate.";
        }
    }

    public async Task ActivateAsync()
    {
        _active = true;
        if (!_loadedOnce || _loadedGame != _game)
        {
            await RefreshAsync();
        }
    }

    public void SetGame(string game)
    {
        if (string.Equals(_game, game, StringComparison.Ordinal))
        {
            return;
        }

        _game = game;
        _loadedOnce = false;
        _loadedGame = string.Empty;
        _hours.Clear();
        Leagues.Clear();
        Opportunities.Clear();
        _selectedLeague = null;
        SelectedOpportunity = null;
        StatusText = "READ-ONLY · NOT LOADED";

        if (_active)
        {
            _ = RefreshAsync();
        }
    }

    public async Task RefreshAsync()
    {
        _cts?.Cancel();
        var cts = new CancellationTokenSource();
        _cts = cts;
        var version = ++_version;

        IsLoading = true;
        StatusText = "LOADING MARKET DATA...";

        try
        {
            var progress = new Progress<FlipLoadProgress>(item =>
            {
                if (version == _version && item.Done < item.Total)
                {
                    StatusText = $"LOADING HOUR {item.Done + 1}/{item.Total}...";
                }
            });

            var hours = await _client.GetHoursAsync(
                HourCount,
                _game == "poe2",
                progress,
                cts.Token);

            if (version != _version)
            {
                return;
            }

            _hours.Clear();
            _hours.AddRange(hours);
            _loadedOnce = hours.Count > 0;
            _loadedGame = _game;

            if (hours.Count == 0)
            {
                Leagues.Clear();
                Opportunities.Clear();
                StatusText = "NO DATA · TRY AGAIN IN A FEW MINUTES";
                NotifyEmpty();
                return;
            }

            PopulateLeagues();
            Analyze();

            var newest = _hours.Max(hour => hour.HourUtc);
            StatusText = $"READ-ONLY · {_hours.Count}H · NEWEST {AgeText(newest)}";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            if (version == _version)
            {
                StatusText = $"MARKET UNAVAILABLE · {exception.Message}";
            }
        }
        finally
        {
            if (version == _version)
            {
                IsLoading = false;
                _cts = null;
            }

            cts.Dispose();
        }
    }

    private void PopulateLeagues()
    {
        var latest = _hours.MaxBy(hour => hour.HourUtc);
        if (latest is null)
        {
            return;
        }

        var ranking = latest.Markets
            .Where(quote => !quote.League.Contains(" (PL", StringComparison.Ordinal))
            .GroupBy(quote => quote.League, StringComparer.Ordinal)
            .Select(group => new
            {
                League = group.Key,
                Volume = group.Sum(quote => (double)quote.VolumeA + quote.VolumeB),
            })
            .OrderByDescending(item => item.Volume)
            .Select(item => item.League)
            .ToList();

        Leagues.Clear();
        foreach (var league in ranking)
        {
            Leagues.Add(league);
        }

        var keep = _selectedLeague is not null && ranking.Contains(_selectedLeague);
        var next = keep
            ? _selectedLeague
            : ranking.FirstOrDefault(league => !IsPermanent(league)) ?? ranking.FirstOrDefault();

        if (next is null)
        {
            return;
        }

        if (string.Equals(_selectedLeague, next, StringComparison.Ordinal))
        {
            return;
        }

        _selectedLeague = next;
        OnPropertyChanged(nameof(SelectedLeague));
    }

    private void Analyze()
    {
        Opportunities.Clear();
        SelectedOpportunity = null;

        if (_selectedLeague is null || _hours.Count == 0)
        {
            NotifyEmpty();
            return;
        }

        var rows = FlipAnalyzer.Analyze(
            _hours,
            new FlipOptions(_selectedLeague, MinVolume, CapitalChaos));

        foreach (var row in rows)
        {
            Opportunities.Add(new FlipRowViewModel(row, _game == "poe2"));
        }

        SelectedOpportunity = Opportunities.FirstOrDefault();
        NotifyEmpty();
    }

    private void NotifyEmpty()
    {
        OnPropertyChanged(nameof(HasOpportunities));
        OnPropertyChanged(nameof(IsEmpty));
    }

    private static bool IsPermanent(string league) =>
        league == "Standard" ||
        league.StartsWith("Hardcore", StringComparison.Ordinal) ||
        league.StartsWith("Ruthless", StringComparison.Ordinal) ||
        league.StartsWith("Solo Self-Found", StringComparison.Ordinal) ||
        league.StartsWith("SSF", StringComparison.Ordinal);

    private static string AgeText(long hourUtc)
    {
        var age = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(hourUtc);
        if (age.TotalHours < 1.5)
        {
            return $"{Math.Max(1, (int)age.TotalMinutes)} MIN AGO";
        }

        return $"{(int)age.TotalHours}H AGO";
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? property = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(property);
    }

    private void OnPropertyChanged([CallerMemberName] string? property = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _client.Dispose();
    }
}

public sealed class FlipRowViewModel
{
    private static readonly IBrush UpBrush = new SolidColorBrush(Color.Parse("#7FE0D0"));
    private static readonly IBrush DownBrush = new SolidColorBrush(Color.Parse("#C98383"));
    private static readonly IBrush FlatBrush = new SolidColorBrush(Color.Parse("#6E7B7A"));

    public FlipRowViewModel(FlipOpportunity opportunity, bool poe2)
    {
        var nameA = Display(CurrencyNames.Short(CurrencyNames.Resolve(opportunity.ItemA, poe2)));
        var nameB = Display(CurrencyNames.Short(CurrencyNames.Resolve(opportunity.ItemB, poe2)));

        Pair = $"{nameA} ⇄ {nameB}";
        RateText = RateSentence(nameA, nameB, opportunity.Rate);
        VolumeText = opportunity.HasChaosValue
            ? $"{Compact(opportunity.VolumeChaosPerHour)} c/h"
            : $"{Compact(opportunity.VolumeChaosPerHour)} u/h";
        Meta = $"spread {opportunity.Spread * 100:0.#}% · {VolumeText}";
        Rating = new string('★', opportunity.Stars);

        var trend = opportunity.Trend;
        TrendText = trend < -0.005
            ? $"▼ {Math.Abs(trend) * 100:0.#}%"
            : trend > 0.005
                ? $"▲ {trend * 100:0.#}%"
                : "—";
        TrendBrush = trend < -0.005 ? DownBrush : trend > 0.005 ? UpBrush : FlatBrush;

        DetailText =
            $"{RateSentence(nameA, nameB, opportunity.Rate)} · spread {opportunity.Spread * 100:0.#}% · {VolumeText} · {opportunity.HoursSeen}h · trend {TrendText}\n" +
            $"Stock {Compact(opportunity.LowStockB)} {nameB} · deploy {Compact(opportunity.DeployableChaos)}c · ceiling {Compact(opportunity.ProfitCeilingChaos)}c · confidence {opportunity.Confidence * 100:0}%\n" +
            $"Buy {nameA} with {nameB}, sell at the higher end. Confirm the live order book in game.";

        Tooltip =
            $"{CurrencyNames.Resolve(opportunity.ItemA, poe2)}\n{CurrencyNames.Resolve(opportunity.ItemB, poe2)}\n" +
            $"{opportunity.MarketId}\nconfidence {opportunity.Confidence * 100:0}% · stability {opportunity.Stability:0.00}";
    }

    public string Pair { get; }

    public string RateText { get; }

    public string Meta { get; }

    public string VolumeText { get; }

    public string Rating { get; }

    public string TrendText { get; }

    public IBrush TrendBrush { get; }

    public string DetailText { get; }

    public string Tooltip { get; }

    private static string Display(string name) =>
        name.StartsWith("Divination Card ", StringComparison.Ordinal)
            ? name["Divination Card ".Length..]
            : name;

    private static string RateSentence(string nameA, string nameB, double rate)
    {
        if (rate <= 0 || !double.IsFinite(rate))
        {
            return "rate unavailable";
        }

        return rate >= 1
            ? $"1 {Unit(nameB)} = {FormatRate(rate)} {Unit(nameA)}"
            : $"1 {Unit(nameA)} = {FormatRate(1 / rate)} {Unit(nameB)}";
    }

    private static string Unit(string name) => name switch
    {
        _ when name.StartsWith("Greater ", StringComparison.Ordinal) => "g" + Unit(name[8..]),
        _ when name.StartsWith("Perfect ", StringComparison.Ordinal) => "p" + Unit(name[8..]),
        "Chaos" => "c",
        "Divine" => "div",
        "Exalted" => "ex",
        "Alchemy" => "alch",
        "Alteration" => "alt",
        "Transmutation" => "trans",
        "Fusing" => "fuse",
        "Jeweller's" => "jew",
        "Chromatic" => "chrom",
        "Scouring" => "scour",
        _ => TrimThe(name),
    };

    private static string TrimThe(string name)
    {
        var value = name.StartsWith("The ", StringComparison.Ordinal) ? name[4..] : name;
        return value.Length <= 10 ? value : value.Split(' ')[0];
    }

    private static string FormatRate(double value) => value switch
    {
        >= 1000 => value.ToString("0", CultureInfo.InvariantCulture),
        >= 100 => value.ToString("0.#", CultureInfo.InvariantCulture),
        >= 10 => value.ToString("0.##", CultureInfo.InvariantCulture),
        >= 1 => value.ToString("0.###", CultureInfo.InvariantCulture),
        _ => value.ToString("0.####", CultureInfo.InvariantCulture),
    };

    private static string Compact(double value) => value switch
    {
        >= 1_000_000 => (value / 1_000_000).ToString("0.#", CultureInfo.InvariantCulture) + "M",
        >= 1_000 => (value / 1_000).ToString("0.#", CultureInfo.InvariantCulture) + "k",
        >= 100 => value.ToString("0", CultureInfo.InvariantCulture),
        >= 10 => value.ToString("0.#", CultureInfo.InvariantCulture),
        _ => value.ToString("0.##", CultureInfo.InvariantCulture),
    };
}
