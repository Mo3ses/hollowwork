namespace Hollowwork.Core.Market;

/// <summary>
/// One hourly snapshot of the public currency exchange, as returned by
/// web.poecdn.com/api/currency-exchange.
/// </summary>
public sealed record CurrencyExchangeHour(
    long HourUtc,
    long NextChangeId,
    IReadOnlyList<MarketQuote> Markets);

/// <summary>
/// A single traded pair. Ratios are expressed as units of <see cref="ItemA"/>
/// per one unit of <see cref="ItemB"/>.
/// </summary>
public sealed record MarketQuote(
    long HourUtc,
    string League,
    string MarketId,
    string ItemA,
    string ItemB,
    long VolumeA,
    long VolumeB,
    long LowStockA,
    long LowStockB,
    long HighStockA,
    long HighStockB,
    long LowRatioA,
    long LowRatioB,
    long HighRatioA,
    long HighRatioB);
