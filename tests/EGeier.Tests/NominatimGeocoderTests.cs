using EGeier.Geocoding;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace EGeier.Tests;

public sealed class NominatimGeocoderTests : IDisposable
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    public void Dispose() => _cache.Dispose();

    private NominatimGeocoder Create(FakeHttpHandler handler, TimeProvider? time = null, NominatimOptions? options = null) =>
        new(new HttpClient(handler), Options.Create(options ?? new NominatimOptions()), _cache, new RequestThrottle(time ?? TimeProvider.System));

    [Fact]
    public async Task Geocode_ParsesFirstResult()
    {
        var geocoder = Create(FakeHttpHandler.Fixture("nominatim-moedling.json"));

        var result = await geocoder.GeocodeAsync("Mödling", TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(48.0855922, result.Latitude, 6);
        Assert.Equal(16.2833526, result.Longitude, 6);
        Assert.StartsWith("Mödling, Bezirk Mödling", result.DisplayName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Geocode_SendsPolicyCompliantRequest()
    {
        var handler = FakeHttpHandler.Fixture("nominatim-moedling.json");
        var geocoder = Create(handler, options: new NominatimOptions { Email = "test@example.org" });

        await geocoder.GeocodeAsync("Mariahilfer Straße 1, Wien", TestContext.Current.CancellationToken);

        var request = handler.Requests.Single();
        var uri = request.RequestUri!.AbsoluteUri;
        Assert.StartsWith("https://nominatim.openstreetmap.org/search?", uri, StringComparison.Ordinal);
        Assert.Contains("q=Mariahilfer%20Stra%C3%9Fe%201%2C%20Wien", uri, StringComparison.Ordinal);
        Assert.Contains("countrycodes=at", uri, StringComparison.Ordinal);
        Assert.Contains("format=jsonv2", uri, StringComparison.Ordinal);
        Assert.Contains("email=test%40example.org", uri, StringComparison.Ordinal);
        Assert.Contains("E-Geier", request.Headers.UserAgent.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Geocode_EmptyResult_ReturnsNull()
    {
        var geocoder = Create(new FakeHttpHandler(_ => FakeHttpHandler.Json("[]")));

        Assert.Null(await geocoder.GeocodeAsync("Atlantis", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Geocode_CachesResults_IncludingNotFound()
    {
        var handler = new FakeHttpHandler(request => FakeHttpHandler.Json(
            request.RequestUri!.Query.Contains("Atlantis", StringComparison.Ordinal) ? "[]" : Fixtures.Read("nominatim-moedling.json")));
        var geocoder = Create(handler);
        var ct = TestContext.Current.CancellationToken;

        await geocoder.GeocodeAsync("Mödling", ct);
        await geocoder.GeocodeAsync("  mödling ", ct);
        await geocoder.GeocodeAsync("Atlantis", ct);
        await geocoder.GeocodeAsync("Atlantis", ct);

        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Geocode_WaitsOneSecondBetweenRequests()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var geocoder = Create(new FakeHttpHandler(_ => FakeHttpHandler.Json("[]")), time);
        var ct = TestContext.Current.CancellationToken;

        await geocoder.GeocodeAsync("a", ct);
        var second = geocoder.GeocodeAsync("b", ct);

        await Task.Delay(50, ct);
        Assert.False(second.IsCompleted);

        time.Advance(TimeSpan.FromMilliseconds(999));
        await Task.Delay(50, ct);
        Assert.False(second.IsCompleted);

        time.Advance(TimeSpan.FromMilliseconds(1));
        await second.WaitAsync(TimeSpan.FromSeconds(5), ct);
    }

    [Fact]
    public async Task Geocode_HttpError_ThrowsGeocodingException()
    {
        var geocoder = Create(new FakeHttpHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.TooManyRequests)));

        var ex = await Assert.ThrowsAsync<GeocodingException>(() => geocoder.GeocodeAsync("Wien", TestContext.Current.CancellationToken));
        Assert.Contains("429", ex.Message, StringComparison.Ordinal);
    }
}
