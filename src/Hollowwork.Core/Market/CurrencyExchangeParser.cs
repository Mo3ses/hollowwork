using System.Text;
using System.Text.Json;

namespace Hollowwork.Core.Market;

public static class CurrencyExchangeParser
{
    public static CurrencyExchangeHour Parse(byte[] utf8, long hourUtc = 0)
    {
        using var document = JsonDocument.Parse(utf8);
        return Parse(document.RootElement, hourUtc);
    }

    public static CurrencyExchangeHour Parse(string json, long hourUtc = 0) =>
        Parse(Encoding.UTF8.GetBytes(json), hourUtc);

    private static CurrencyExchangeHour Parse(JsonElement root, long hourUtc)
    {
        var nextChangeId = root.TryGetProperty("next_change_id", out var next) &&
                           next.TryGetInt64(out var nextValue)
            ? nextValue
            : 0;

        var markets = new List<MarketQuote>();
        if (!root.TryGetProperty("markets", out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return new CurrencyExchangeHour(hourUtc, nextChangeId, markets);
        }

        foreach (var market in array.EnumerateArray())
        {
            var quote = ParseMarket(market, hourUtc);
            if (quote is not null)
            {
                markets.Add(quote);
            }
        }

        return new CurrencyExchangeHour(hourUtc, nextChangeId, markets);
    }

    private static MarketQuote? ParseMarket(JsonElement market, long hourUtc)
    {
        var league = Text(market, "league");
        var marketId = Text(market, "market_id");
        if (league.Length == 0 || marketId.Length == 0)
        {
            return null;
        }

        var itemA = string.Empty;
        var itemB = string.Empty;
        if (market.TryGetProperty("market_pair", out var pair) &&
            pair.ValueKind == JsonValueKind.Array &&
            pair.GetArrayLength() >= 2)
        {
            itemA = pair[0].GetString() ?? string.Empty;
            itemB = pair[1].GetString() ?? string.Empty;
        }

        if (itemA.Length == 0 || itemB.Length == 0)
        {
            var parts = marketId.Split('|');
            if (parts.Length != 2)
            {
                return null;
            }

            itemA = parts[0];
            itemB = parts[1];
        }

        if (itemA.Length == 0 || itemB.Length == 0 || string.Equals(itemA, itemB, StringComparison.Ordinal))
        {
            return null;
        }

        var volume = Object(market, "volume_traded");
        var lowStock = Object(market, "lowest_stock");
        var highStock = Object(market, "highest_stock");
        var lowRatio = Object(market, "lowest_ratio");
        var highRatio = Object(market, "highest_ratio");

        return new MarketQuote(
            hourUtc,
            league,
            marketId,
            itemA,
            itemB,
            Number(volume, itemA),
            Number(volume, itemB),
            Number(lowStock, itemA),
            Number(lowStock, itemB),
            Number(highStock, itemA),
            Number(highStock, itemB),
            Number(lowRatio, itemA),
            Number(lowRatio, itemB),
            Number(highRatio, itemA),
            Number(highRatio, itemB));
    }

    private static JsonElement Object(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object
            ? value
            : default;

    private static string Text(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static long Number(JsonElement parent, string name)
    {
        if (parent.ValueKind != JsonValueKind.Object ||
            !parent.TryGetProperty(name, out var value))
        {
            return 0;
        }

        if (value.TryGetInt64(out var integer))
        {
            return integer;
        }

        return value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number)
            ? (long)number
            : 0;
    }
}
