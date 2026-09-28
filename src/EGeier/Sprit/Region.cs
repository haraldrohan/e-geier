using System.Text.Json.Serialization;

namespace EGeier.Sprit;

/// <summary>
/// A region usable for <see cref="ISpritClient.SearchByRegionAsync"/>: a Bundesland with its
/// Bezirke as <see cref="SubRegions"/>.
/// </summary>
public sealed record Region
{
    /// <summary>Region code, e.g. 3 for Niederösterreich or 317 for Bezirk Mödling.</summary>
    public long Code { get; init; }

    /// <summary>State or district.</summary>
    public RegionType Type { get; init; }

    /// <summary>Name as delivered by the API, e.g. "Graz(Stadt)" or "Wien 10.,Favoriten".</summary>
    public required string Name { get; init; }

    /// <summary>Districts of a state; empty for districts.</summary>
    public IReadOnlyList<Region> SubRegions { get; init; } = [];

    /// <summary>Postal codes within the region.</summary>
    public IReadOnlyList<string> PostalCodes { get; init; } = [];

    /// <summary>Municipalities within the region; only filled when requested with <c>includeCities</c>.</summary>
    public IReadOnlyList<string> Cities { get; init; } = [];
}

/// <summary>A Bundesland from the administrative units list.</summary>
public sealed record AdministrativeState
{
    /// <summary>State code.</summary>
    [JsonPropertyName("c")]
    public long Code { get; init; }

    /// <summary>State name.</summary>
    [JsonPropertyName("n")]
    public required string Name { get; init; }

    /// <summary>Districts.</summary>
    [JsonPropertyName("b")]
    public IReadOnlyList<AdministrativeDistrict> Districts { get; init; } = [];
}

/// <summary>A Bezirk from the administrative units list.</summary>
public sealed record AdministrativeDistrict
{
    /// <summary>District code.</summary>
    [JsonPropertyName("c")]
    public long Code { get; init; }

    /// <summary>District name.</summary>
    [JsonPropertyName("n")]
    public required string Name { get; init; }

    /// <summary>Municipalities with coordinates.</summary>
    [JsonPropertyName("g")]
    public IReadOnlyList<Municipality> Municipalities { get; init; } = [];
}

/// <summary>A municipality (Gemeinde) with postal code and coordinates.</summary>
public sealed record Municipality
{
    /// <summary>Postal code.</summary>
    [JsonPropertyName("p")]
    public string? PostalCode { get; init; }

    /// <summary>Name.</summary>
    [JsonPropertyName("n")]
    public required string Name { get; init; }

    /// <summary>Latitude (WGS 84).</summary>
    [JsonPropertyName("b")]
    public double Latitude { get; init; }

    /// <summary>Longitude (WGS 84).</summary>
    [JsonPropertyName("l")]
    public double Longitude { get; init; }
}
