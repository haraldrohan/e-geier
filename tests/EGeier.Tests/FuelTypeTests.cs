using EGeier.Sprit;

namespace EGeier.Tests;

public class FuelTypeTests
{
    [Theory]
    [InlineData(FuelType.Diesel, "DIE")]
    [InlineData(FuelType.Super95, "SUP")]
    [InlineData(FuelType.Cng, "GAS")]
    public void ApiCodes_RoundTrip(FuelType fuelType, string code)
    {
        Assert.Equal(code, fuelType.ToApiCode());
        Assert.True(FuelTypes.TryParseApiCode(code, out var parsed));
        Assert.Equal(fuelType, parsed);
    }

    [Theory]
    [InlineData("Diesel", FuelType.Diesel)]
    [InlineData("die", FuelType.Diesel)]
    [InlineData("Super", FuelType.Super95)]
    [InlineData("Super 95", FuelType.Super95)]
    [InlineData("Benzin", FuelType.Super95)]
    [InlineData("petrol", FuelType.Super95)]
    [InlineData("CNG", FuelType.Cng)]
    [InlineData("Erdgas", FuelType.Cng)]
    public void TryParse_AcceptsCommonNames(string input, FuelType expected)
    {
        Assert.True(FuelTypes.TryParse(input, out var parsed));
        Assert.Equal(expected, parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("LPG")]
    [InlineData("Strom")]
    public void TryParse_RejectsUnknown(string? input)
    {
        Assert.False(FuelTypes.TryParse(input, out _));
    }
}
