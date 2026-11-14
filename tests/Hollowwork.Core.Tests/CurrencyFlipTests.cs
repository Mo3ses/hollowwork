using Hollowwork.Core.Market;

namespace Hollowwork.Core.Tests;

public class CurrencyFlipTests
{
    private const string Chaos = "Metadata/Items/Currency/CurrencyRerollRare";
    private const string Divine = "Metadata/Items/Currency/CurrencyModValues";

    [Fact]
    public void Parses_market_pair_and_ratios()
    {
        const string json = """
            {
              "next_change_id": 200,
              "markets": [
                {
                  "league": "Test",
                  "market_id": "Metadata/Items/Currency/CurrencyRerollRare|Metadata/Items/Currency/CurrencyModValues",
                  "market_pair": [
                    "Metadata/Items/Currency/CurrencyRerollRare",
                    "Metadata/Items/Currency/CurrencyModValues"
                  ],
                  "volume_traded": {
                    "Metadata/Items/Currency/CurrencyRerollRare": 85000,
                    "Metadata/Items/Currency/CurrencyModValues": 1000
                  },
                  "lowest_stock": {
                    "Metadata/Items/Currency/CurrencyRerollRare": 5000,
                    "Metadata/Items/Currency/CurrencyModValues": 10
                  },
                  "highest_stock": {
                    "Metadata/Items/Currency/CurrencyRerollRare": 6000,
                    "Metadata/Items/Currency/CurrencyModValues": 12
                  },
                  "lowest_ratio": {
                    "Metadata/Items/Currency/CurrencyRerollRare": 80,
                    "Metadata/Items/Currency/CurrencyModValues": 1
                  },
                  "highest_ratio": {
                    "Metadata/Items/Currency/CurrencyRerollRare": 90,
                    "Metadata/Items/Currency/CurrencyModValues": 1
                  }
                },
                {
                  "league": "Other",
                  "market_id": "x|y",
                  "market_pair": ["x", "y"]
                }
              ]
            }
            """;

        var hour = CurrencyExchangeParser.Parse(json, 123);

        Assert.Equal(123, hour.HourUtc);
        Assert.Equal(200, hour.NextChangeId);
        Assert.Equal(2, hour.Markets.Count);

        var quote = hour.Markets[0];
        Assert.Equal("Test", quote.League);
        Assert.Equal(Chaos, quote.ItemA);
        Assert.Equal(Divine, quote.ItemB);
        Assert.Equal(85000, quote.VolumeA);
        Assert.Equal(1000, quote.VolumeB);
        Assert.Equal(80, quote.LowRatioA);
        Assert.Equal(1, quote.LowRatioB);
        Assert.Equal(90, quote.HighRatioA);
        Assert.Equal(1, quote.HighRatioB);
    }

    [Fact]
    public void Ranks_profitable_pairs_and_estimates_ceiling()
    {
        var hours = new[]
        {
            Hour(100,
                Quote(100, Chaos, Divine, 80, 90, volumeA: 85_000, volumeB: 1_000, lowStockB: 10)),
            Hour(200,
                Quote(200, Chaos, Divine, 80, 90, volumeA: 85_000, volumeB: 1_000, lowStockB: 10)),
        };

        var rows = FlipAnalyzer.Analyze(
            hours,
            new FlipOptions("Test", MinVolumePerHour: 100, CapitalChaos: 500));

        var row = Assert.Single(rows);
        Assert.Equal(85, row.Rate, 3);
        Assert.Equal(0.125, row.Spread, 3);
        Assert.Equal(500, row.DeployableChaos, 3);
        Assert.Equal(62.5, row.ProfitCeilingChaos, 3);
        Assert.InRange(row.Stars, 3, 5);
        Assert.Equal(2, row.HoursSeen);
    }

    [Fact]
    public void Filters_quiet_pairs()
    {
        var hours = new[]
        {
            Hour(100, Quote(100, Chaos, Divine, 80, 90, volumeA: 40, volumeB: 1, lowStockB: 10)),
        };

        var rows = FlipAnalyzer.Analyze(
            hours,
            new FlipOptions("Test", MinVolumePerHour: 100));

        Assert.Empty(rows);
    }

    [Fact]
    public void Wide_hourly_spread_is_capped()
    {
        var hours = new[]
        {
            Hour(100, Quote(100, Chaos, Divine, 1, 2, volumeA: 85_000, volumeB: 1_000, lowStockB: 10)),
        };

        var row = Assert.Single(FlipAnalyzer.Analyze(
            hours,
            new FlipOptions("Test", MinVolumePerHour: 100, CapitalChaos: 500)));

        Assert.True(row.Stars <= 3, $"stars={row.Stars} confidence={row.Confidence:0.00} spread={row.Spread:0.00}");
    }

    [Fact]
    public void Falling_prices_are_excluded()
    {
        var hours = new[]
        {
            Hour(100, Quote(100, Chaos, Divine, 105, 115, volumeA: 85_000, volumeB: 1_000, lowStockB: 10)),
            Hour(200, Quote(200, Chaos, Divine, 80, 90, volumeA: 85_000, volumeB: 1_000, lowStockB: 10)),
        };

        var rows = FlipAnalyzer.Analyze(
            hours,
            new FlipOptions("Test", MinVolumePerHour: 100, CapitalChaos: 500));

        Assert.Empty(rows);
    }

    [Fact]
    public void Resolves_common_names_and_falls_back_to_camel_case()
    {
        Assert.Equal("Chaos Orb", CurrencyNames.Resolve(Chaos));
        Assert.Equal("Chaos", CurrencyNames.Short("Chaos Orb"));
        Assert.Equal("Regal Orb", CurrencyNames.Resolve(
            "Metadata/Items/Currency/CurrencyUpgradeToRare", poe2: true));
        Assert.Equal("Greater Chaos Orb", CurrencyNames.Resolve(
            "Metadata/Items/Currency/CurrencyRerollRare2", poe2: true));
        Assert.Equal("Vivid Lifeforce", CurrencyNames.Resolve("Metadata/Items/Currency/HarvestSeedGreen"));
        Assert.Equal("Some New Currency", CurrencyNames.Resolve("Metadata/Items/Currency/CurrencySomeNewCurrency"));
    }

    private static CurrencyExchangeHour Hour(long hour, params MarketQuote[] quotes) =>
        new(hour, hour + 3600, quotes);

    private static MarketQuote Quote(
        long hour,
        string itemA,
        string itemB,
        long lowRateA,
        long highRateA,
        long volumeA,
        long volumeB,
        long lowStockB) =>
        new(
            hour,
            "Test",
            $"{itemA}|{itemB}",
            itemA,
            itemB,
            volumeA,
            volumeB,
            volumeA * 10,
            lowStockB,
            volumeA * 10,
            lowStockB + 2,
            lowRateA,
            1,
            highRateA,
            1);
}
