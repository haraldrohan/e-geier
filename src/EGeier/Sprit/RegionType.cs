using System.Text.Json.Serialization;

namespace EGeier.Sprit;

/// <summary>Kind of administrative region used by the region search.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RegionType>))]
public enum RegionType
{
    /// <summary>Bundesland (federal state, API code <c>BL</c>).</summary>
    [JsonStringEnumMemberName("BL")]
    State,

    /// <summary>Politischer Bezirk (district, API code <c>PB</c>).</summary>
    [JsonStringEnumMemberName("PB")]
    District,
}

/// <summary>Conversions for <see cref="RegionType"/>.</summary>
public static class RegionTypes
{
    /// <summary>Returns the code the API expects, <c>BL</c> or <c>PB</c>.</summary>
    public static string ToApiCode(this RegionType regionType) => regionType switch
    {
        RegionType.State => "BL",
        RegionType.District => "PB",
        _ => throw new ArgumentOutOfRangeException(nameof(regionType), regionType, null),
    };
}
