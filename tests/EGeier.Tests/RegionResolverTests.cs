using System.Text.Json;
using EGeier.Sprit;

namespace EGeier.Tests;

public class RegionResolverTests
{
    private static readonly IReadOnlyList<Region> Regions =
        JsonSerializer.Deserialize<List<Region>>(Fixtures.Read("regions-with-cities.json"), JsonSerializerOptions.Web)!;

    [Theory]
    [InlineData("Steiermark", 6, RegionType.State)]
    [InlineData("niederösterreich", 3, RegionType.State)]
    [InlineData("Niederoesterreich", 3, RegionType.State)]
    [InlineData("Wien", 9, RegionType.State)]
    [InlineData("Salzburg", 5, RegionType.State)]
    [InlineData("Salzburg Stadt", 501, RegionType.District)]
    [InlineData("Mödling", 317, RegionType.District)]
    [InlineData("Bezirk Mödling", 317, RegionType.District)]
    [InlineData("moedling", 317, RegionType.District)]
    [InlineData("Graz", 601, RegionType.District)]
    [InlineData("Graz-Umgebung", 606, RegionType.District)]
    [InlineData("Linz", 401, RegionType.District)]
    [InlineData("Steyr-Land", 415, RegionType.District)]
    [InlineData("St. Pölten", 302, RegionType.District)]
    [InlineData("Klagenfurt", 201, RegionType.District)]
    public void Resolve_ExactNames(string query, long expectedCode, RegionType expectedType)
    {
        var match = Assert.Single(RegionResolver.Resolve(Regions, query));

        Assert.Equal(RegionMatchKind.ExactName, match.MatchedBy);
        Assert.Equal(expectedCode, match.Region.Code);
        Assert.Equal(expectedType, match.Region.Type);
    }

    [Fact]
    public void Resolve_ViennaDistrictByPartialName()
    {
        var match = Assert.Single(RegionResolver.Resolve(Regions, "Favoriten"));

        Assert.Equal(910, match.Region.Code);
        Assert.Equal(RegionMatchKind.PartialName, match.MatchedBy);
    }

    [Fact]
    public void Resolve_ViennaDistrictByNumber()
    {
        var match = Assert.Single(RegionResolver.Resolve(Regions, "Wien 1"));

        Assert.Equal(901, match.Region.Code);
    }

    [Fact]
    public void Resolve_MunicipalityToDistrict()
    {
        var match = Assert.Single(RegionResolver.Resolve(Regions, "Perchtoldsdorf"));

        Assert.Equal(317, match.Region.Code);
        Assert.Equal(RegionMatchKind.Municipality, match.MatchedBy);
    }

    [Fact]
    public void Resolve_PostalCodeToDistrict()
    {
        var matches = RegionResolver.Resolve(Regions, "2340");

        Assert.Equal(317, matches[0].Region.Code);
        Assert.All(matches, m => Assert.Equal(RegionMatchKind.PostalCode, m.MatchedBy));
    }

    [Fact]
    public void Resolve_AmbiguousPrefix_ReturnsAllCandidates()
    {
        var matches = RegionResolver.Resolve(Regions, "Wiener");

        Assert.True(matches.Count > 1);
        Assert.Contains(matches, m => m.Region.Name == "Wiener Neustadt(Stadt)");
        Assert.Contains(matches, m => m.Region.Name == "Wiener Neustadt(Land)");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Atlantis")]
    public void Resolve_Unknown_ReturnsEmpty(string query)
    {
        Assert.Empty(RegionResolver.Resolve(Regions, query));
    }
}
