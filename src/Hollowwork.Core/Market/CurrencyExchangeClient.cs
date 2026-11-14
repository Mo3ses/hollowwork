using System.Net;
using System.Net.Http.Headers;

namespace Hollowwork.Core.Market;

public sealed record FlipLoadProgress(int Done, int Total);

/// <summary>
/// Read-only client for the public hourly currency exchange aggregate.
/// Responses are cached on disk so changing leagues or reopening the window
/// does not hit the API again.
/// </summary>
public sealed class CurrencyExchangeClient : IDisposable
{
    private const string Endpoint = "https://web.poecdn.com/api/currency-exchange";
    private readonly HttpClient _http;
    private readonly string? _cacheRoot;

    public CurrencyExchangeClient(string? cacheRoot = null)
    {
        _cacheRoot = cacheRoot;

        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        };

        _http = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(45),
        };

        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Hollowwork/0.1 (+https://github.com/Mo3ses/hollowwork)");
        _http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public static long HourOf(DateTimeOffset when, int lagHours = 1)
    {
        var seconds = when.ToUnixTimeSeconds();
        return seconds - seconds % 3600 - lagHours * 3600L;
    }

    public static long LatestHour(int lagHours = 1) => HourOf(DateTimeOffset.UtcNow, lagHours);

    public async Task<CurrencyExchangeHour?> GetHourAsync(
        long hourUtc,
        bool poe2,
        CancellationToken cancellationToken = default)
    {
        var cachePath = CachePath(hourUtc, poe2);
        if (cachePath is not null && File.Exists(cachePath))
        {
            try
            {
                var cached = await File.ReadAllBytesAsync(cachePath, cancellationToken);
                if (cached.Length > 0)
                {
                    var parsed = CurrencyExchangeParser.Parse(cached, hourUtc);
                    if (parsed.Markets.Count > 0)
                    {
                        return parsed;
                    }
                }

                TryDelete(cachePath);
            }
            catch (IOException)
            {
                // A corrupt cache entry is not fatal; fetch a fresh copy below.
            }
            catch (System.Text.Json.JsonException)
            {
                TryDelete(cachePath);
            }
        }

        var url = BuildUrl(hourUtc, poe2);
        using var response = await _http.GetAsync(url, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Currency exchange returned {(int)response.StatusCode} for hour {hourUtc}.");
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (bytes.Length == 0)
        {
            return null;
        }

        var hour = CurrencyExchangeParser.Parse(bytes, hourUtc);
        if (hour.Markets.Count == 0)
        {
            return null;
        }

        if (cachePath is not null)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
                await File.WriteAllBytesAsync(cachePath, bytes, cancellationToken);
            }
            catch (IOException)
            {
                // Caching is an optimization; ignore disk failures.
            }
        }

        return hour;
    }

    public async Task<IReadOnlyList<CurrencyExchangeHour>> GetHoursAsync(
        int count,
        bool poe2,
        IProgress<FlipLoadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (count <= 0)
        {
            return [];
        }

        var latest = LatestHour();
        var hours = new List<CurrencyExchangeHour>(count);

        for (var index = 0; index < count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new FlipLoadProgress(index, count));

            var hourUtc = latest - index * 3600L;
            var cached = IsCached(hourUtc, poe2);
            var hour = await GetHourAsync(hourUtc, poe2, cancellationToken);
            if (hour is not null && hour.Markets.Count > 0)
            {
                hours.Add(hour);
            }

            if (!cached && index < count - 1)
            {
                await Task.Delay(150, cancellationToken);
            }
        }

        progress?.Report(new FlipLoadProgress(count, count));
        return hours;
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
    }

    private bool IsCached(long hourUtc, bool poe2)
    {
        var path = CachePath(hourUtc, poe2);
        return path is not null && File.Exists(path);
    }

    private string? CachePath(long hourUtc, bool poe2)
    {
        if (string.IsNullOrWhiteSpace(_cacheRoot))
        {
            return null;
        }

        return Path.Combine(_cacheRoot, poe2 ? "poe2" : "poe1", $"{hourUtc}.json");
    }

    private static string BuildUrl(long hourUtc, bool poe2) =>
        $"{Endpoint}{(poe2 ? "/poe2" : string.Empty)}/{hourUtc}";

    public void Dispose() => _http.Dispose();
}
