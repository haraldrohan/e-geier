using System.Net;
using EGeier.Sprit;
using Microsoft.Extensions.Time.Testing;

namespace EGeier.Tests;

public class SpritClientTests
{
    private static (SpritClient Client, FakeHttpHandler Handler) Create(FakeHttpHandler handler, TimeProvider? time = null) =>
        (new SpritClient(new HttpClient(handler), time), handler);

    [Fact]
    public async Task SearchByLocation_BuildsQueryWithInvariantNumbers()
    {
        var (client, handler) = Create(FakeHttpHandler.Fixture("by-address-moedling-diesel.json"));

        await client.SearchByLocationAsync(48.0833, 16.2833, FuelType.Diesel, includeClosed: true, TestContext.Current.CancellationToken);

        Assert.Equal(
            "https://api.e-control.at/sprit/1.0/search/gas-stations/by-address?latitude=48.0833&longitude=16.2833&fuelType=DIE&includeClosed=true",
            handler.Requests.Single().RequestUri!.ToString());
    }

    [Fact]
    public async Task SearchByLocation_ParsesStations()
    {
        var (client, _) = Create(FakeHttpHandler.Fixture("by-address-moedling-diesel.json"));

        var stations = await client.SearchByLocationAsync(48.0833, 16.2833, FuelType.Diesel, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(10, stations.Count);
        var first = stations[0];
        Assert.Equal(218712, first.Id);
        Assert.Equal("SPRITKÖNIG SCS", first.Name);
        Assert.Equal(1, first.Position);
        Assert.True(first.IsOpen);
        Assert.Equal(4.05, first.DistanceKm, 2);
        Assert.Equal(2.199m, first.GetPrice(FuelType.Diesel));
        Assert.Null(first.GetPrice(FuelType.Super95));
        Assert.Equal("Vösendorf", first.Location!.City);
        Assert.Equal("2334", first.Location.PostalCode);
        Assert.Equal(48.112274, first.Location.Latitude, 6);
        Assert.Equal(8, first.OpeningHours.Count);
        Assert.Equal(OpeningDay.PublicHoliday, first.OpeningHours[^1].Day);
        Assert.Equal("24:00", first.OpeningHours[0].To);
        Assert.True(first.PaymentMethods!.CreditCard);
        Assert.True(first.OfferInformation!.SelfService);

        // Only the cheapest stations carry prices.
        Assert.Equal(5, stations.Count(s => s.Prices.Count > 0));
    }

    [Fact]
    public async Task SearchByLocation_IncludeClosed_ParsesClosedStationsAndClubCards()
    {
        var (client, _) = Create(FakeHttpHandler.Fixture("by-address-moedling-super-closed.json"));

        var stations = await client.SearchByLocationAsync(48.0833, 16.2833, FuelType.Super95, true, TestContext.Current.CancellationToken);

        Assert.Contains(stations, s => !s.IsOpen);
        Assert.Contains(stations, s => s.PaymentArrangements is { ClubCard: true, ClubCardText: "ARBÖ" });
        Assert.All(stations.SelectMany(s => s.Prices), p => Assert.Equal(FuelType.Super95, p.FuelType));
    }

    [Fact]
    public async Task SearchByRegion_BuildsQuery()
    {
        var (client, handler) = Create(FakeHttpHandler.Fixture("by-region-317-diesel.json"));

        var stations = await client.SearchByRegionAsync(317, RegionType.District, FuelType.Diesel, cancellationToken: TestContext.Current.CancellationToken);

        Assert.EndsWith(
            "/search/gas-stations/by-region?code=317&type=PB&fuelType=DIE&includeClosed=false",
            handler.Requests.Single().RequestUri!.ToString());
        Assert.Equal(5, stations.Count);
        Assert.All(stations, s => Assert.Equal(0, s.DistanceKm));
    }

    [Fact]
    public async Task GetRegions_ParsesHierarchyWithCities()
    {
        var (client, handler) = Create(FakeHttpHandler.Fixture("regions-with-cities.json"));

        var regions = await client.GetRegionsAsync(includeCities: true, TestContext.Current.CancellationToken);

        Assert.EndsWith("/regions?includeCities=true", handler.Requests.Single().RequestUri!.ToString());
        Assert.Equal(9, regions.Count);
        Assert.All(regions, r => Assert.Equal(RegionType.State, r.Type));
        var moedling = regions.Single(r => r.Code == 3).SubRegions.Single(d => d.Code == 317);
        Assert.Equal(RegionType.District, moedling.Type);
        Assert.Equal("Mödling", moedling.Name);
        Assert.Contains("Perchtoldsdorf", moedling.Cities);
        Assert.Contains("2340", moedling.PostalCodes);
    }

    [Fact]
    public async Task GetAdministrativeUnits_ParsesShortPropertyNames()
    {
        var (client, _) = Create(FakeHttpHandler.Fixture("regions-units.json"));

        var units = await client.GetAdministrativeUnitsAsync(TestContext.Current.CancellationToken);

        var eisenstadt = units[0].Districts[0].Municipalities[0];
        Assert.Equal("Burgenland", units[0].Name);
        Assert.Equal("Eisenstadt", eisenstadt.Name);
        Assert.Equal("7000", eisenstadt.PostalCode);
        Assert.Equal(47.844055, eisenstadt.Latitude, 6);
        Assert.Equal(16.5272, eisenstadt.Longitude, 6);
    }

    [Fact]
    public async Task BadRequest_ExposesApiErrors()
    {
        var (client, _) = Create(FakeHttpHandler.Fixture("error-400.json", HttpStatusCode.BadRequest));

        var ex = await Assert.ThrowsAsync<SpritApiException>(() =>
            client.SearchByLocationAsync(48, 16, FuelType.Diesel, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Equal("SPE_G01", ex.ErrorCode);
        Assert.Contains("latitude should be of type java.lang.Double", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(7, 10, 0, true)]   // summer (UTC+2): 12:00 Vienna
    [InlineData(7, 10, 15, true)]  // 12:15 Vienna
    [InlineData(7, 10, 30, false)] // 12:30 Vienna
    [InlineData(7, 12, 5, false)]  // 14:05 Vienna
    [InlineData(1, 11, 5, true)]   // winter (UTC+1): 12:05 Vienna
    public async Task ServerError_AroundNoon_ExplainsPriceUpdateWindow(int month, int utcHour, int utcMinute, bool expectNoon)
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, month, 1, utcHour, utcMinute, 0, TimeSpan.Zero));
        var (client, _) = Create(new FakeHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)), time);

        var ex = await Assert.ThrowsAsync<SpritApiException>(() =>
            client.GetRegionsAsync(cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, ex.StatusCode);
        Assert.Equal(expectNoon, ex.DuringNoonPriceUpdate);
        Assert.Equal(expectNoon, ex.Message.Contains("12:00", StringComparison.Ordinal));
    }

    [Fact]
    public async Task NetworkFailure_IsWrapped()
    {
        var (client, _) = Create(new FakeHttpHandler(_ => throw new HttpRequestException("boom")));

        var ex = await Assert.ThrowsAsync<SpritApiException>(() => client.PingAsync(TestContext.Current.CancellationToken));

        Assert.IsType<HttpRequestException>(ex.InnerException);
        Assert.Null(ex.StatusCode);
    }

    [Fact]
    public async Task HtmlMaintenancePage_IsReportedAsUnreadable()
    {
        var (client, _) = Create(new FakeHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>Wartung</html>"),
        }));

        await Assert.ThrowsAsync<SpritApiException>(() =>
            client.GetRegionsAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(91, 16)]
    [InlineData(48, 181)]
    public async Task SearchByLocation_RejectsInvalidCoordinates(double lat, double lon)
    {
        var (client, _) = Create(FakeHttpHandler.Fixture("by-address-moedling-diesel.json"));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.SearchByLocationAsync(lat, lon, FuelType.Diesel, cancellationToken: TestContext.Current.CancellationToken));
    }
}
