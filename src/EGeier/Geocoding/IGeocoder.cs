namespace EGeier.Geocoding;

/// <summary>Turns a place name or address into coordinates.</summary>
public interface IGeocoder
{
    /// <summary>
    /// Returns the best match for <paramref name="query"/>, or <see langword="null"/> if nothing was found.
    /// </summary>
    /// <param name="query">Place name or address, e.g. "Mödling" or "Mariahilfer Straße 1, Wien".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<GeocodingResult?> GeocodeAsync(string query, CancellationToken cancellationToken = default);
}

/// <summary>A geocoded place.</summary>
/// <param name="Latitude">Latitude (WGS 84).</param>
/// <param name="Longitude">Longitude (WGS 84).</param>
/// <param name="DisplayName">Full name of the match, e.g. "Mödling, Bezirk Mödling, Niederösterreich, 2340, Österreich".</param>
public sealed record GeocodingResult(double Latitude, double Longitude, string DisplayName);

/// <summary>Raised when the geocoding service fails.</summary>
public sealed class GeocodingException : Exception
{
    /// <summary>Creates an exception.</summary>
    public GeocodingException()
    {
    }

    /// <summary>Creates an exception with a message.</summary>
    public GeocodingException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception with a message and inner exception.</summary>
    public GeocodingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
