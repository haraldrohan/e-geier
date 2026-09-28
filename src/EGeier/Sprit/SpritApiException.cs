using System.Net;

namespace EGeier.Sprit;

/// <summary>Raised when the Spritpreisrechner API cannot be reached or returns an error.</summary>
public sealed class SpritApiException : Exception
{
    /// <summary>Creates an exception with a message.</summary>
    public SpritApiException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception with a message and inner exception.</summary>
    public SpritApiException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates an exception.</summary>
    public SpritApiException()
    {
    }

    /// <summary>HTTP status code, if a response was received.</summary>
    public HttpStatusCode? StatusCode { get; init; }

    /// <summary>API error code such as <c>SPE_G01</c>, if the API returned one.</summary>
    public string? ErrorCode { get; init; }

    /// <summary>Validation messages returned by the API.</summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>
    /// <see langword="true"/> if the failure happened around 12:00 Vienna time, when fuel prices
    /// may be raised and the API is regularly unavailable for a few minutes.
    /// </summary>
    public bool DuringNoonPriceUpdate { get; init; }
}
