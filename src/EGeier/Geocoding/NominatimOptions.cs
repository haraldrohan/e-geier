namespace EGeier.Geocoding;

/// <summary>Settings for <see cref="NominatimGeocoder"/>.</summary>
/// <remarks>
/// The public Nominatim service has a usage policy
/// (<see href="https://operations.osmfoundation.org/policies/nominatim/"/>): at most one request per
/// second, an identifying User-Agent, and caching of results. The defaults comply with it.
/// </remarks>
public sealed class NominatimOptions
{
    /// <summary>Base address of the public OpenStreetMap Nominatim service.</summary>
    public static readonly Uri PublicBaseAddress = new("https://nominatim.openstreetmap.org/");

    /// <summary>Nominatim endpoint. Change it to use a self-hosted instance.</summary>
    public Uri BaseAddress { get; set; } = PublicBaseAddress;

    /// <summary>User-Agent identifying the application, required by the Nominatim usage policy.</summary>
    public string UserAgent { get; set; } = "E-Geier (+https://github.com/haraldrohan/e-geier)";

    /// <summary>Optional contact e-mail sent with each request, as recommended for heavier use.</summary>
    public string? Email { get; set; }

    /// <summary>ISO 3166-1 country codes to restrict results to. Default: Austria.</summary>
    public string CountryCodes { get; set; } = "at";

    /// <summary>Preferred language for display names.</summary>
    public string AcceptLanguage { get; set; } = "de";

    /// <summary>Minimum time between two requests. The public service allows one per second.</summary>
    public TimeSpan MinimumInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>How long results (including "not found") are cached.</summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromHours(24);
}
