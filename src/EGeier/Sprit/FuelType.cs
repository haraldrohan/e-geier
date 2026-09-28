using System.Diagnostics.CodeAnalysis;

namespace EGeier.Sprit;

/// <summary>Fuel types supported by the Spritpreisrechner.</summary>
public enum FuelType
{
    /// <summary>Diesel (API code <c>DIE</c>).</summary>
    Diesel,

    /// <summary>Super 95 (API code <c>SUP</c>).</summary>
    Super95,

    /// <summary>Compressed natural gas, CNG (API code <c>GAS</c>).</summary>
    Cng,
}

/// <summary>Conversions between <see cref="FuelType"/> and API codes or user input.</summary>
public static class FuelTypes
{
    /// <summary>All supported fuel types.</summary>
    public static IReadOnlyList<FuelType> All { get; } = [FuelType.Diesel, FuelType.Super95, FuelType.Cng];

    /// <summary>Returns the code the API expects, e.g. <c>DIE</c>.</summary>
    public static string ToApiCode(this FuelType fuelType) => fuelType switch
    {
        FuelType.Diesel => "DIE",
        FuelType.Super95 => "SUP",
        FuelType.Cng => "GAS",
        _ => throw new ArgumentOutOfRangeException(nameof(fuelType), fuelType, null),
    };

    /// <summary>Returns a German display name, e.g. <c>Super 95</c>.</summary>
    public static string ToDisplayName(this FuelType fuelType) => fuelType switch
    {
        FuelType.Diesel => "Diesel",
        FuelType.Super95 => "Super 95",
        FuelType.Cng => "Erdgas (CNG)",
        _ => throw new ArgumentOutOfRangeException(nameof(fuelType), fuelType, null),
    };

    /// <summary>Parses an API code (<c>DIE</c>, <c>SUP</c>, <c>GAS</c>).</summary>
    public static bool TryParseApiCode(string? code, out FuelType fuelType)
    {
        switch (code?.Trim().ToUpperInvariant())
        {
            case "DIE": fuelType = FuelType.Diesel; return true;
            case "SUP": fuelType = FuelType.Super95; return true;
            case "GAS": fuelType = FuelType.Cng; return true;
            default: fuelType = default; return false;
        }
    }

    /// <summary>
    /// Parses loose user input in German or English, e.g. "diesel", "Super", "Benzin", "95", "CNG", "Erdgas",
    /// as well as the API codes.
    /// </summary>
    public static bool TryParse([NotNullWhen(true)] string? input, out FuelType fuelType)
    {
        if (TryParseApiCode(input, out fuelType))
        {
            return true;
        }

        var normalized = input?.Trim().ToLowerInvariant().Replace(" ", "", StringComparison.Ordinal);
        switch (normalized)
        {
            case "diesel":
                fuelType = FuelType.Diesel;
                return true;
            case "super" or "super95" or "95" or "benzin" or "petrol" or "gasoline" or "unleaded" or "bleifrei" or "eurosuper":
                fuelType = FuelType.Super95;
                return true;
            case "cng" or "erdgas" or "gas" or "naturalgas" or "biomethan":
                fuelType = FuelType.Cng;
                return true;
            default:
                fuelType = default;
                return false;
        }
    }
}
