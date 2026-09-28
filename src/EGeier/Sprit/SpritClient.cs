using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EGeier.Sprit;

/// <summary>Default <see cref="ISpritClient"/> implementation on top of <see cref="HttpClient"/>.</summary>
/// <remarks>
/// Register it with <c>services.AddSpritClient()</c>, or construct it directly with your own
/// <see cref="HttpClient"/>. If the client has no <see cref="HttpClient.BaseAddress"/>,
/// <see cref="DefaultBaseAddress"/> is used.
/// </remarks>
public sealed class SpritClient : ISpritClient
{
    /// <summary>Base address of the public API.</summary>
    public static readonly Uri DefaultBaseAddress = new("https://api.e-control.at/sprit/1.0/");

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private readonly HttpClient _httpClient;
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates a client.</summary>
    /// <param name="httpClient">HTTP client, typically provided by <c>IHttpClientFactory</c>.</param>
    /// <param name="timeProvider">Clock used to detect the noon price update window.</param>
    public SpritClient(HttpClient httpClient, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<GasStation>> SearchByLocationAsync(
        double latitude,
        double longitude,
        FuelType fuelType,
        bool includeClosed = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(latitude, -90);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(latitude, 90);
        ArgumentOutOfRangeException.ThrowIfLessThan(longitude, -180);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(longitude, 180);

        var query = string.Create(
            CultureInfo.InvariantCulture,
            $"search/gas-stations/by-address?latitude={latitude}&longitude={longitude}&fuelType={fuelType.ToApiCode()}&includeClosed={Bool(includeClosed)}");
        return GetJsonAsync<IReadOnlyList<GasStation>>(query, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<GasStation>> SearchByRegionAsync(
        long regionCode,
        RegionType regionType,
        FuelType fuelType,
        bool includeClosed = false,
        CancellationToken cancellationToken = default)
    {
        var query = string.Create(
            CultureInfo.InvariantCulture,
            $"search/gas-stations/by-region?code={regionCode}&type={regionType.ToApiCode()}&fuelType={fuelType.ToApiCode()}&includeClosed={Bool(includeClosed)}");
        return GetJsonAsync<IReadOnlyList<GasStation>>(query, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Region>> GetRegionsAsync(bool includeCities = false, CancellationToken cancellationToken = default) =>
        GetJsonAsync<IReadOnlyList<Region>>($"regions?includeCities={Bool(includeCities)}", cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<AdministrativeState>> GetAdministrativeUnitsAsync(CancellationToken cancellationToken = default) =>
        GetJsonAsync<IReadOnlyList<AdministrativeState>>("regions/units", cancellationToken);

    /// <inheritdoc />
    public Task<string> PingAsync(CancellationToken cancellationToken = default) =>
        GetStringAsync("ping", cancellationToken);

    /// <inheritdoc />
    public Task<string> GetMonitoringAsync(CancellationToken cancellationToken = default) =>
        GetStringAsync("monitoring", cancellationToken);

    private static string Bool(bool value) => value ? "true" : "false";

    private Uri BuildUri(string relative) => new(_httpClient.BaseAddress ?? DefaultBaseAddress, relative);

    private async Task<T> GetJsonAsync<T>(string relative, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(relative, cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken).ConfigureAwait(false);
            return result ?? throw new SpritApiException("The Spritpreisrechner API returned an empty response.");
        }
        catch (JsonException ex)
        {
            throw new SpritApiException("The Spritpreisrechner API returned a response that could not be read.", ex)
            {
                StatusCode = response.StatusCode,
                DuringNoonPriceUpdate = IsNoonPriceUpdateWindow(),
            };
        }
    }

    private async Task<string> GetStringAsync(string relative, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(relative, cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendAsync(string relative, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(BuildUri(relative), cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw Unavailable(null, ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw Unavailable(null, ex);
        }

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                throw await BadRequestAsync(response, cancellationToken).ConfigureAwait(false);
            }

            throw Unavailable(response.StatusCode, null);
        }
    }

    private SpritApiException Unavailable(HttpStatusCode? statusCode, Exception? inner)
    {
        var noon = IsNoonPriceUpdateWindow();
        var message = noon
            ? "The Spritpreisrechner API is temporarily unavailable. Around 12:00 (Austrian time) fuel prices may be " +
              "raised and the service is regularly offline for a few minutes. Please try again after about 12:15."
            : statusCode is { } code
                ? $"The Spritpreisrechner API is currently unavailable (HTTP {(int)code}). Please try again later."
                : "The Spritpreisrechner API could not be reached. Please check the internet connection or try again later.";

        return inner is null
            ? new SpritApiException(message) { StatusCode = statusCode, DuringNoonPriceUpdate = noon }
            : new SpritApiException(message, inner) { StatusCode = statusCode, DuringNoonPriceUpdate = noon };
    }

    private static async Task<SpritApiException> BadRequestAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        ApiError? error = null;
        try
        {
            error = await response.Content.ReadFromJsonAsync<ApiError>(JsonOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            // Fall through to a generic message.
        }

        var errors = error?.Errors ?? [];
        var details = errors.Count > 0 ? ": " + string.Join("; ", errors) : ".";
        return new SpritApiException("The Spritpreisrechner API rejected the request" + details)
        {
            StatusCode = response.StatusCode,
            ErrorCode = error?.Code,
            Errors = errors,
        };
    }

    /// <summary>Prices may only be raised at 12:00; the API is often briefly offline around then.</summary>
    internal bool IsNoonPriceUpdateWindow()
    {
        var local = TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), Vienna).TimeOfDay;
        return local >= new TimeSpan(11, 58, 0) && local <= new TimeSpan(12, 20, 0);
    }

    private sealed record ApiError(string? Code, string? Name, IReadOnlyList<string>? Errors);
}
