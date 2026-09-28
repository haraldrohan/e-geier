namespace EGeier.Tests;

public class GeoMathTests
{
    [Fact]
    public void DistanceKm_ViennaToGraz()
    {
        // Stephansdom → Grazer Hauptplatz, roughly 145 km as the crow flies.
        var km = GeoMath.DistanceKm(48.2085, 16.3731, 47.0707, 15.4382);

        Assert.InRange(km, 143, 147);
    }

    [Fact]
    public void DistanceKm_MatchesApiDistance()
    {
        // The API reported 4.05 km from the search point to SPRITKÖNIG SCS.
        var km = GeoMath.DistanceKm(48.0833, 16.2833, 48.112274, 16.316303);

        Assert.Equal(4.05, km, 1);
    }
}
