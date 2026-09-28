using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace EGeier.Geocoding;

/// <summary>
/// <see cref="IGeocoder"/> backed by OpenStreetMap Nominatim, limited to Austria by default,
/// with a one-request-per-second throttle and result caching.
/// </summary>
public sealed class NominatimGeocoder : IGeocoder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private readonly HttpClient _httpClient;
    private readonly NominatimOptions _options;
    private readonly IMemoryCache _cache;
    private readonly RequestThrottle _throttle;

    /// <summary>Creates a geocoder.</summary>
    /// <param name="httpClient">HTTP client, typically provided by <c>IHttpClientFactory</c>.</param>
    /// <param name="options">Settings.</param>
    /// <param name="cache">Cache for results; share one instance so repeated queries do not hit Nominatim.</param>
    public NominatimGeocoder(HttpClient httpClient, IOptions<NominatimOptions> options, IMemoryCache cache)
        : this(httpClient, options, cache, RequestThrottle.Shared)
    {
    }

    internal NominatimGeocoder(HttpClient httpClient, IOptions<NominatimOptions> options, IMemoryCache cache, RequestThrottle throttle)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(cache);
        _httpClient = httpClient;
        _options = options.Value;
        _cache = cache;
        _throttle = throttle;

        if (string.IsNullOrWhiteSpace(_options.UserAgent))
        {
            throw new ArgumentException("Nominatim requires an identifying User-Agent.", nameof(options));
        }
    }

    /// <inheritdoc />
    public async Task<GeocodingResult?> GeocodeAsync(string query, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        var normalized = string.Join(' ', query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var cacheKey = $"EGeier:Nominatim:{_options.CountryCodes}:{normalized.ToUpperInvariant()}";

        if (_cache.TryGetValue(cacheKey, out GeocodingResult? cached))
        {
            return cached;
        }

        var result = await _throttle
            .RunAsync(_options.MinimumInterval, () => QueryAsync(normalized, cancellationToken), cancellationToken)
            .ConfigureAwait(false);

        _cache.Set(cacheKey, result, _options.CacheDuration);
        return result;
    }

    private async Task<GeocodingResult?> QueryAsync(string query, CancellationToken cancellationToken)
    {
        var uri = new Uri(_options.BaseAddress, "search?" + BuildQueryString(query));
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd(_options.UserAgent);
        request.Headers.AcceptLanguage.ParseAdd(_options.AcceptLanguage);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new GeocodingException(
                    $"The geocoding service (OpenStreetMap Nominatim) returned HTTP {(int)response.StatusCode}. Please try again later.");
            }

            var places = await response.Content
                .ReadFromJsonAsync<List<NominatimPlace>>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            return places is [var first, ..]
                ? new GeocodingResult(first.Lat, first.Lon, first.DisplayName ?? query)
                : null;
        }
        catch (HttpRequestException ex)
        {
            throw new GeocodingException("The geocoding service (OpenStreetMap Nominatim) could not be reached.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new GeocodingException("The geocoding service (OpenStreetMap Nominatim) did not respond in time.", ex);
        }
        catch (JsonException ex)
        {
            throw new GeocodingException("The geocoding service (OpenStreetMap Nominatim) returned an unreadable response.", ex);
        }
    }

    private string BuildQueryString(string query)
    {
        var parameters = new List<KeyValuePair<string, string>>
        {
            new("q", query),
            new("format", "jsonv2"),
            new("limit", "1"),
        };
        if (!string.IsNullOrWhiteSpace(_options.CountryCodes))
        {
            parameters.Add(new("countrycodes", _options.CountryCodes));
        }

        if (!string.IsNullOrWhiteSpace(_options.Email))
        {
            parameters.Add(new("email", _options.Email));
        }

        return string.Join('&', parameters.Select(p =>
            string.Create(CultureInfo.InvariantCulture, $"{p.Key}={Uri.EscapeDataString(p.Value)}")));
    }

    private sealed record NominatimPlace(
        double Lat,
        double Lon,
        [property: JsonPropertyName("display_name")] string? DisplayName);
}
