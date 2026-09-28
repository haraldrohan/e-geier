using System.Net;
using System.Text.Json;
using EGeier.Geocoding;
using EGeier.Mcp;
using EGeier.Sprit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ModelContextProtocol;

namespace EGeier.Tests;

public sealed class SpritToolsTests : IDisposable
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 9, 28, 20, 0, 0, TimeSpan.Zero));
    private readonly ServiceProvider _services;
    private readonly SpritTools _tools;

    public SpritToolsTests()
    {
        var handler = new FakeHttpHandler(request => request.RequestUri!.AbsolutePath switch
        {
            var p when p.EndsWith("/by-address", StringComparison.Ordinal) => FakeHttpHandler.Json(Fixtures.Read("by-address-moedling-diesel.json")),
            var p when p.EndsWith("/by-region", StringComparison.Ordinal) => FakeHttpHandler.Json(Fixtures.Read("by-region-317-diesel.json")),
            var p when p.EndsWith("/regions", StringComparison.Ordinal) => FakeHttpHandler.Json(Fixtures.Read("regions-with-cities.json")),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        });

        var sprit = new SpritClient(new HttpClient(handler), _time);
        _services = new ServiceCollection().AddSingleton<ISpritClient>(sprit).BuildServiceProvider();
        var regions = new RegionDirectory(_services.GetRequiredService<IServiceScopeFactory>(), _time);
        _tools = new SpritTools(sprit, new FakeGeocoder(), regions, _time);
    }

    public void Dispose() => _services.Dispose();

    [Fact]
    public async Task FindCheapestStations_ReturnsCompactResult()
    {
        var json = await _tools.FindCheapestStations("Mödling", "Diesel", cancellationToken: TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("Diesel", root.GetProperty("fuel_type").GetString());
        Assert.Equal("2026-09-28 22:00 (Austrian time)", root.GetProperty("retrieved_at").GetString());
        var stations = root.GetProperty("stations");
        Assert.Equal(5, stations.GetArrayLength());
        var first = stations[0];
        Assert.Equal("SPRITKÖNIG SCS", first.GetProperty("name").GetString());
        Assert.Equal(2.199m, first.GetProperty("price_eur").GetDecimal());
        Assert.Equal("Westring Area 1, 2334 Vösendorf", first.GetProperty("address").GetString());
        Assert.Equal(4.1, first.GetProperty("distance_km").GetDouble());
        Assert.True(first.GetProperty("open").GetBoolean());
        Assert.StartsWith("https://www.openstreetmap.org/directions?", first.GetProperty("map_url").GetString(), StringComparison.Ordinal);
        Assert.Equal(5, root.GetProperty("other_stations_without_price").GetArrayLength());
        Assert.Contains("not affiliated", root.GetProperty("source").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task FindCheapestStations_UnknownPlace_ReturnsHelpfulError()
    {
        var ex = await Assert.ThrowsAsync<McpException>(() =>
            _tools.FindCheapestStations("Atlantis", "diesel", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("Could not find \"Atlantis\"", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FindCheapestStations_UnknownFuel_ReturnsHelpfulError()
    {
        var ex = await Assert.ThrowsAsync<McpException>(() =>
            _tools.FindCheapestStations("Mödling", "Strom", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("\"diesel\", \"super\" or \"cng\"", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FindStationsByRegion_ResolvesNameAndOmitsDistance()
    {
        var json = await _tools.FindStationsByRegion("Bezirk Mödling", "diesel", cancellationToken: TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("Bezirk Mödling", root.GetProperty("search_area").GetString());
        var first = root.GetProperty("stations")[0];
        Assert.False(first.TryGetProperty("distance_km", out _));
        Assert.StartsWith("https://www.openstreetmap.org/?mlat=", first.GetProperty("map_url").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task FindStationsByRegion_Municipality_ExplainsDistrict()
    {
        var json = await _tools.FindStationsByRegion("Perchtoldsdorf", "diesel", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("\\\"Perchtoldsdorf\\\" is in Bezirk Mödling", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FindStationsByRegion_Ambiguous_ListsCandidates()
    {
        var ex = await Assert.ThrowsAsync<McpException>(() =>
            _tools.FindStationsByRegion("Wiener", "diesel", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("Wiener Neustadt(Stadt)", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListRegions_GroupsDistrictsByState()
    {
        var json = await _tools.ListRegions(TestContext.Current.CancellationToken);
        var regions = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json)!;

        Assert.Equal(9, regions.Count);
        Assert.Contains("Mödling", regions["Niederösterreich"]);
    }

    private sealed class FakeGeocoder : IGeocoder
    {
        public Task<GeocodingResult?> GeocodeAsync(string query, CancellationToken cancellationToken = default) =>
            Task.FromResult(query == "Mödling"
                ? new GeocodingResult(48.0833, 16.2833, "Mödling, Bezirk Mödling, Niederösterreich, 2340, Österreich")
                : null);
    }
}
