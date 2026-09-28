using EGeier.Geocoding;
using EGeier.Sprit;
using Microsoft.Extensions.DependencyInjection;

namespace EGeier.IntegrationTests;

/// <summary>
/// Calls the real Spritpreisrechner API and Nominatim. Skipped unless the environment variable
/// <c>EGEIER_LIVE_TESTS=1</c> is set, so that the normal test run stays offline.
/// </summary>
public sealed class LiveApiTests : IDisposable
{
    private readonly ServiceProvider _services;

    public LiveApiTests()
    {
        Assert.SkipUnless(
            Environment.GetEnvironmentVariable("EGEIER_LIVE_TESTS") == "1",
            "Live tests are disabled. Set EGEIER_LIVE_TESTS=1 to run them.");

        var services = new ServiceCollection();
        services.AddSpritClient();
        services.AddNominatimGeocoder();
        _services = services.BuildServiceProvider();
    }

    public void Dispose() => _services.Dispose();

    private ISpritClient Sprit => _services.GetRequiredService<ISpritClient>();

    [Fact]
    public async Task Ping()
    {
        var message = await Sprit.PingAsync(TestContext.Current.CancellationToken);

        Assert.Contains("Spritpreisrechner", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchByLocation_Vienna()
    {
        var stations = await Sprit.SearchByLocationAsync(48.2085, 16.3731, FuelType.Diesel, cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotEmpty(stations);
        Assert.Contains(stations, s => s.GetPrice(FuelType.Diesel) is > 0.5m and < 5m);
        Assert.All(stations, s => Assert.InRange(s.DistanceKm, 0, 50));
    }

    [Theory]
    [InlineData(3, RegionType.State, FuelType.Super95)]
    [InlineData(317, RegionType.District, FuelType.Diesel)]
    public async Task SearchByRegion(long code, RegionType type, FuelType fuelType)
    {
        var stations = await Sprit.SearchByRegionAsync(code, type, fuelType, cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotEmpty(stations);
        Assert.All(stations, s => Assert.NotNull(s.GetPrice(fuelType)));
    }

    [Fact]
    public async Task Regions_ResolveEveryDistrictByItsOwnName()
    {
        var regions = await Sprit.GetRegionsAsync(includeCities: true, TestContext.Current.CancellationToken);

        Assert.Equal(9, regions.Count);
        foreach (var district in regions.SelectMany(r => r.SubRegions))
        {
            var match = RegionResolver.Resolve(regions, district.Name);
            Assert.True(match.Count > 0 && match[0].Region.Code == district.Code, $"'{district.Name}' did not resolve to itself.");
        }
    }

    [Fact]
    public async Task AdministrativeUnits()
    {
        var units = await Sprit.GetAdministrativeUnitsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(9, units.Count);
        Assert.All(units.SelectMany(u => u.Districts).SelectMany(d => d.Municipalities), m => Assert.InRange(m.Latitude, 46, 49.1));
    }

    [Fact]
    public async Task Geocode_Moedling()
    {
        var geocoder = _services.GetRequiredService<IGeocoder>();

        var result = await geocoder.GeocodeAsync("Mödling", TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(48.08, result.Latitude, 1);
        Assert.Equal(16.28, result.Longitude, 1);
    }
}
