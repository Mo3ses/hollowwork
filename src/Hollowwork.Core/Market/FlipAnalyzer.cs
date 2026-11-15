namespace Hollowwork.Core.Market;

public sealed record FlipOptions(
    string League,
    int MinVolumePerHour = 100,
    double CapitalChaos = 500,
    int MaxResults = 40,
    int MinStars = 1,
    double MaxSpread = double.PositiveInfinity,
    double MinTrend = double.NegativeInfinity,
    string? Category = null);

/// <summary>
/// One ranked pair. All monetary values are estimates derived from hourly
/// aggregates; the live order book in game is the only source of truth for
/// an actual trade.
/// </summary>
public sealed record FlipOpportunity(
    string MarketId,
    string ItemA,
    string ItemB,
    double Rate,
    double LowRate,
    double HighRate,
    double Spread,
    double RangeWidth,
    double Confidence,
    double Stability,
    double Trend,
    double VolumeA,
    double VolumeB,
    double VolumeChaosPerHour,
    double LowStockA,
    double LowStockB,
    double PriceA,
    double PriceB,
    double DeployableChaos,
    double ProfitCeilingChaos,
    int HoursSeen,
    int Stars,
    double Score,
    string Category = "other")
{
    public bool HasChaosValue => PriceB > 0;
}

/// <summary>
/// Turns hourly exchange snapshots into ranked market-making candidates.
/// The scoring is deliberately conservative and documented: spread is a
/// theoretical ceiling, confidence falls when the hourly range is wide or
/// history is short, and falling prices are penalized.
/// </summary>
public static class FlipAnalyzer
{
    public const string ChaosPath = "Metadata/Items/Currency/CurrencyRerollRare";

    // One mispriced order can make the hourly low/high useless. Ignore those
    // hours when estimating a rate and when building the chaos price graph.
    private const double MaxHourlyRange = 3.0;

    public static IReadOnlyList<FlipOpportunity> Analyze(
        IReadOnlyList<CurrencyExchangeHour> hours,
        FlipOptions options)
    {
        if (hours.Count == 0 || string.IsNullOrWhiteSpace(options.League))
        {
            return [];
        }

        var groups = new Dictionary<string, List<MarketQuote>>(StringComparer.Ordinal);
        foreach (var hour in hours)
        {
            foreach (var quote in hour.Markets)
            {
                if (!string.Equals(quote.League, options.League, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!groups.TryGetValue(quote.MarketId, out var list))
                {
                    list = [];
                    groups[quote.MarketId] = list;
                }

                list.Add(quote);
            }
        }

        if (groups.Count == 0)
        {
            return [];
        }

        var representatives = new List<(MarketQuote Quote, double Mid)>();
        foreach (var list in groups.Values)
        {
            var clean = list
                .Where(IsReasonable)
                .OrderBy(quote => quote.HourUtc)
                .ToList();

            if (clean.Count == 0)
            {
                continue;
            }

            var latest = clean[^1];
            var mid = Median(clean.Select(MidRate).ToList());
            representatives.Add((latest, mid));
        }

        var prices = BuildChaosPrices(representatives);
        var results = new List<FlipOpportunity>(groups.Count);

        foreach (var group in groups.Values)
        {
            var quotes = group
                .OrderBy(quote => quote.HourUtc)
                .ToList();

            var opportunity = AnalyzePair(quotes, prices, options);
            if (opportunity is not null)
            {
                results.Add(opportunity);
            }
        }

        return results
            .OrderByDescending(item => item.Stars)
            .ThenByDescending(item => item.Score)
            .ThenByDescending(item => item.VolumeChaosPerHour)
            .Take(Math.Max(1, options.MaxResults))
            .ToList();
    }

    public static double MidRate(MarketQuote quote)
    {
        var low = Rate(quote.LowRatioA, quote.LowRatioB);
        var high = Rate(quote.HighRatioA, quote.HighRatioB);
        return low > 0 && high > 0 ? (low + high) / 2 : 0;
    }

    private static bool IsReasonable(MarketQuote quote)
    {
        var low = Rate(quote.LowRatioA, quote.LowRatioB);
        var high = Rate(quote.HighRatioA, quote.HighRatioB);
        return low > 0 && high >= low && high / low <= MaxHourlyRange;
    }

    private static FlipOpportunity? AnalyzePair(
        IReadOnlyList<MarketQuote> quotes,
        IReadOnlyDictionary<string, double> prices,
        FlipOptions options)
    {
        var hourly = new List<(MarketQuote Quote, double Mid, double Spread)>(quotes.Count);
        foreach (var quote in quotes)
        {
            var low = Rate(quote.LowRatioA, quote.LowRatioB);
            var high = Rate(quote.HighRatioA, quote.HighRatioB);
            if (low <= 0 || high <= 0 || high < low)
            {
                continue;
            }

            var spread = high / low - 1;
            if (spread > MaxHourlyRange)
            {
                continue;
            }

            var mid = (low + high) / 2;
            if (mid <= 0 || spread <= 0)
            {
                continue;
            }

            hourly.Add((quote, mid, spread));
        }

        if (hourly.Count == 0)
        {
            return null;
        }

        var latest = hourly[^1];
        var category = CategoryOf(latest.Quote.ItemA, latest.Quote.ItemB);
        if (!string.IsNullOrEmpty(options.Category) &&
            !string.Equals(category, options.Category, StringComparison.Ordinal))
        {
            return null;
        }

        var medianSpread = Median(hourly.Select(item => item.Spread).ToList());
        if (medianSpread < 0.005)
        {
            return null;
        }

        var rangeWidth = medianSpread + 1;
        var priceA = prices.GetValueOrDefault(latest.Quote.ItemA);
        var priceB = prices.GetValueOrDefault(latest.Quote.ItemB);

        var volumeA = latest.Quote.VolumeA * priceA;
        var volumeB = latest.Quote.VolumeB * priceB;
        var hasChaosValue = priceA > 0 || priceB > 0;
        var volumeChaos = hasChaosValue
            ? Math.Max(volumeA, volumeB)
            : Math.Max(latest.Quote.VolumeA, latest.Quote.VolumeB);

        if (volumeChaos < options.MinVolumePerHour)
        {
            return null;
        }

        var deployable = priceB > 0
            ? Math.Min(options.CapitalChaos, Math.Max(0, latest.Quote.LowStockB) * priceB)
            : 0;

        var profitCeiling = deployable * medianSpread;
        var stability = Stability(hourly);
        var confidence = Confidence(hourly.Count, rangeWidth, volumeChaos);
        if (stability > 0.6)
        {
            confidence = Math.Min(confidence, 0.35);
        }

        if (medianSpread > 1.0)
        {
            confidence = Math.Min(confidence, 0.35);
        }
        else if (medianSpread > 0.5)
        {
            confidence = Math.Min(confidence, 0.55);
        }

        var trend = 0d;
        if (hourly.Count > 1)
        {
            var half = hourly.Count / 2;
            var first = Median(hourly.Take(half).Select(item => item.Mid).ToList());
            var last = Median(hourly.Skip(hourly.Count - half).Select(item => item.Mid).ToList());
            if (first > 0)
            {
                trend = last / first - 1;
            }
        }

        var stars = 1 + (int)Math.Round(confidence * 4, MidpointRounding.AwayFromZero);
        if (trend < -0.15 || rangeWidth >= 3)
        {
            stars = Math.Min(stars, 2);
        }

        stars = Math.Clamp(stars, 1, 5);

        // Show only rows the analyzer actually trusts. Wide hourly ranges are
        // usually one mispriced order, not a tradable spread.
        if (stars < 3 || stars < options.MinStars)
        {
            return null;
        }

        if (medianSpread > options.MaxSpread || trend < options.MinTrend)
        {
            return null;
        }

        var score = medianSpread * Math.Sqrt(Math.Max(volumeChaos, 1)) * confidence * confidence;
        if (trend < 0)
        {
            score *= Math.Max(0.25, 1 + trend);
        }

        var rate = Median(hourly.Select(item => item.Mid).ToList());
        return new FlipOpportunity(
            latest.Quote.MarketId,
            latest.Quote.ItemA,
            latest.Quote.ItemB,
            rate,
            Rate(latest.Quote.LowRatioA, latest.Quote.LowRatioB),
            Rate(latest.Quote.HighRatioA, latest.Quote.HighRatioB),
            medianSpread,
            rangeWidth,
            confidence,
            stability,
            trend,
            volumeA,
            volumeB,
            volumeChaos,
            latest.Quote.LowStockA,
            latest.Quote.LowStockB,
            priceA,
            priceB,
            deployable,
            profitCeiling,
            hourly.Count,
            stars,
            score,
            category);
    }

    public static string CategoryOf(string itemA, string itemB)
    {
        var text = itemA + "|" + itemB;
        if (text.Contains("DivinationCard", StringComparison.OrdinalIgnoreCase))
        {
            return "cards";
        }

        if (text.Contains("UberBossKey", StringComparison.OrdinalIgnoreCase) ||
            (text.Contains("MapFragments", StringComparison.OrdinalIgnoreCase) &&
             text.Contains("Key", StringComparison.OrdinalIgnoreCase)))
        {
            return "keys";
        }

        if (text.Contains("/Gems/", StringComparison.OrdinalIgnoreCase))
        {
            return "gems";
        }

        if (text.Contains("/Currency/", StringComparison.OrdinalIgnoreCase))
        {
            return "currency";
        }

        return "other";
    }

    private static double Confidence(int hoursSeen, double rangeWidth, double volumeChaos)
    {
        var tightness = rangeWidth <= 1.01
            ? 1
            : Math.Clamp(1 - (rangeWidth - 1) / 2, 0, 1);
        var hoursTrust = Math.Clamp(hoursSeen / 4.0, 0, 1);
        var volumeTrust = Math.Clamp(Math.Log10(Math.Max(volumeChaos, 1)) / 4.0, 0, 1);

        var confidence = 0.5 * tightness + 0.2 * hoursTrust + 0.3 * volumeTrust;
        return rangeWidth >= 3 ? Math.Min(confidence, 0.35) : confidence;
    }

    private static double Stability(IReadOnlyList<(MarketQuote Quote, double Mid, double Spread)> hourly)
    {
        if (hourly.Count < 2)
        {
            return 0;
        }

        var mean = hourly.Average(item => item.Mid);
        if (mean <= 0)
        {
            return 0;
        }

        var variance = hourly.Average(item => (item.Mid - mean) * (item.Mid - mean));
        return Math.Sqrt(variance) / mean;
    }

    private static Dictionary<string, double> BuildChaosPrices(
        IReadOnlyList<(MarketQuote Quote, double Mid)> quotes)
    {
        var edges = new Dictionary<string, Dictionary<string, (double Factor, double Volume)>>(
            StringComparer.Ordinal);

        foreach (var (quote, rate) in quotes)
        {
            if (rate <= 0 || !double.IsFinite(rate))
            {
                continue;
            }

            var volume = Math.Max(quote.VolumeA, quote.VolumeB);
            AddEdge(edges, quote.ItemB, quote.ItemA, rate, volume);
            AddEdge(edges, quote.ItemA, quote.ItemB, 1 / rate, volume);
        }

        var prices = new Dictionary<string, double>(StringComparer.Ordinal);
        if (!edges.ContainsKey(ChaosPath))
        {
            return prices;
        }

        prices[ChaosPath] = 1;
        var bestCost = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            [ChaosPath] = 0,
        };
        var queue = new PriorityQueue<string, double>();
        queue.Enqueue(ChaosPath, 0);

        while (queue.TryDequeue(out var from, out var cost))
        {
            if (bestCost.TryGetValue(from, out var known) && cost > known)
            {
                continue;
            }

            foreach (var (to, edge) in edges[from])
            {
                if (edge.Factor <= 0 || !double.IsFinite(edge.Factor))
                {
                    continue;
                }

                // Prefer liquid conversions over a cheap but almost empty pair.
                var nextCost = cost + 1.0 / Math.Max(edge.Volume, 1);
                if (bestCost.TryGetValue(to, out var existing) && existing <= nextCost)
                {
                    continue;
                }

                var price = prices[from] / edge.Factor;
                if (!double.IsFinite(price) || price <= 0)
                {
                    continue;
                }

                bestCost[to] = nextCost;
                prices[to] = price;
                queue.Enqueue(to, nextCost);
            }
        }

        return prices;
    }

    private static void AddEdge(
        Dictionary<string, Dictionary<string, (double Factor, double Volume)>> edges,
        string from,
        string to,
        double factor,
        double volume)
    {
        if (from.Length == 0 || to.Length == 0 || factor <= 0 || !double.IsFinite(factor))
        {
            return;
        }

        if (!edges.TryGetValue(from, out var targets))
        {
            targets = new Dictionary<string, (double Factor, double Volume)>(StringComparer.Ordinal);
            edges[from] = targets;
        }

        if (!targets.TryGetValue(to, out var existing) || existing.Volume < volume)
        {
            targets[to] = (factor, volume);
        }
    }

    private static double Rate(long numerator, long denominator) =>
        denominator > 0 ? (double)numerator / denominator : 0;

    private static double Median(List<double> values)
    {
        values.Sort();
        var middle = values.Count / 2;
        return values.Count % 2 == 0
            ? (values[middle - 1] + values[middle]) / 2
            : values[middle];
    }
}
