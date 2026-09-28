using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using EGeier.Geocoding;
using EGeier.Sprit;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace EGeier.Mcp;

/// <summary>Read-only MCP tools for the Spritpreisrechner.</summary>
[McpServerToolType]
[SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "MCP parameter names are snake_case.")]
internal sealed class SpritTools(ISpritClient sprit, IGeocoder geocoder, RegionDirectory regions, TimeProvider timeProvider)
{
    private const string FuelTypeDescription =
        "Fuel type: \"diesel\", \"super\" (Super 95 petrol/Benzin) or \"cng\" (Erdgas). German and English names are accepted.";

    private const string PriceNote =
        "By law the Spritpreisrechner only publishes prices for the cheapest stations of a search; stations listed " +
        "without a price are more expensive. Prices in EUR per litre (CNG: per kg).";

    private const string Source =
        "Data: public Spritpreisrechner API of E-Control Austria. E-Geier is unofficial and not affiliated with " +
        "E-Control Austria; no guarantee of accuracy or timeliness.";

    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    [McpServerTool(Name = "find_cheapest_stations", Title = "Find cheapest gas stations near a place", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description(
        "Finds the cheapest gas stations in Austria near a place or address, e.g. \"Mödling\", " +
        "\"Mariahilfer Straße 1, Wien\" or \"8010 Graz\". Returns price, name, address, straight-line distance, " +
        "whether the station is open, and a map link. Only Austrian locations are supported.")]
    public async Task<string> FindCheapestStations(
        [Description("Place name or address in Austria.")] string location,
        [Description(FuelTypeDescription)] string fuel_type,
        [Description("Also include stations that are currently closed. Default: false.")] bool include_closed = false,
        CancellationToken cancellationToken = default)
    {
        var fuelType = ParseFuelType(fuel_type);
        if (string.IsNullOrWhiteSpace(location))
        {
            throw new McpException("Please provide a place name or address in Austria.");
        }

        GeocodingResult? place;
        try
        {
            place = await geocoder.GeocodeAsync(location, cancellationToken).ConfigureAwait(false);
        }
        catch (GeocodingException ex)
        {
            throw new McpException(ex.Message, ex);
        }

        if (place is null)
        {
            throw new McpException(
                $"Could not find \"{location}\" in Austria. Try a town name, a postal code or a more complete address.");
        }

        var stations = await CallApiAsync(() => sprit.SearchByLocationAsync(
            place.Latitude, place.Longitude, fuelType, include_closed, cancellationToken)).ConfigureAwait(false);

        return Serialize(BuildResult(
            stations,
            fuelType,
            origin: place,
            searchArea: place.DisplayName,
            regionNote: null));
    }

    [McpServerTool(Name = "find_stations_by_region", Title = "Find cheapest gas stations in a state or district", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description(
        "Finds the cheapest gas stations in an Austrian Bundesland (state) or Bezirk (district), e.g. " +
        "\"Steiermark\", \"Bezirk Mödling\", \"Graz\" or \"Favoriten\". A municipality name or postal code is " +
        "resolved to its district. Use list_regions to see all valid names.")]
    public async Task<string> FindStationsByRegion(
        [Description("Name of a Bundesland or Bezirk (or a municipality / postal code within it).")] string region,
        [Description(FuelTypeDescription)] string fuel_type,
        [Description("Also include stations that are currently closed. Default: false.")] bool include_closed = false,
        CancellationToken cancellationToken = default)
    {
        var fuelType = ParseFuelType(fuel_type);
        var all = await CallApiAsync(() => regions.GetRegionsAsync(cancellationToken)).ConfigureAwait(false);
        var matches = RegionResolver.Resolve(all, region);

        if (matches.Count == 0)
        {
            throw new McpException(
                $"Unknown region \"{region}\". Use list_regions for valid Bundesland and Bezirk names, or use " +
                "find_cheapest_stations for a specific place.");
        }

        if (matches.Count > 1 && matches[0].MatchedBy == RegionMatchKind.PartialName)
        {
            var candidates = string.Join(", ", matches.Take(10).Select(m => m.Region.Name));
            throw new McpException($"\"{region}\" is ambiguous. Did you mean one of: {candidates}?");
        }

        var match = matches[0];
        var regionNote = match.MatchedBy switch
        {
            RegionMatchKind.Municipality or RegionMatchKind.PostalCode =>
                $"\"{region}\" is in {DescribeRegion(match.Region)}; showing the cheapest stations of the whole district. " +
                "Use find_cheapest_stations for stations closest to the place.",
            _ => null,
        };

        var stations = await CallApiAsync(() => sprit.SearchByRegionAsync(
            match.Region.Code, match.Region.Type, fuelType, include_closed, cancellationToken)).ConfigureAwait(false);

        return Serialize(BuildResult(
            stations,
            fuelType,
            origin: null,
            searchArea: DescribeRegion(match.Region),
            regionNote));
    }

    [McpServerTool(Name = "list_regions", Title = "List Austrian states and districts", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Lists all Austrian Bundesländer (states) with their Bezirke (districts), as accepted by find_stations_by_region.")]
    public async Task<string> ListRegions(CancellationToken cancellationToken = default)
    {
        var all = await CallApiAsync(() => regions.GetRegionsAsync(cancellationToken)).ConfigureAwait(false);
        var result = all.ToDictionary(s => s.Name, s => s.SubRegions.Select(d => d.Name).ToList());
        return Serialize(result);
    }

    internal static FuelType ParseFuelType(string? input) =>
        FuelTypes.TryParse(input, out var fuelType)
            ? fuelType
            : throw new McpException($"Unknown fuel type \"{input}\". Use \"diesel\", \"super\" or \"cng\".");

    private static string DescribeRegion(Region region) =>
        region.Type == RegionType.State ? $"Bundesland {region.Name}" : $"Bezirk {region.Name}";

    private static async Task<T> CallApiAsync<T>(Func<Task<T>> call)
    {
        try
        {
            return await call().ConfigureAwait(false);
        }
        catch (SpritApiException ex)
        {
            throw new McpException(ex.Message, ex);
        }
    }

    private StationSearchResult BuildResult(
        IReadOnlyList<GasStation> stations,
        FuelType fuelType,
        GeocodingResult? origin,
        string searchArea,
        string? regionNote)
    {
        var withPrice = new List<StationSummary>();
        var withoutPrice = new List<StationSummary>();

        foreach (var station in stations.OrderBy(s => s.Position))
        {
            var price = station.GetPrice(fuelType);
            var summary = new StationSummary(
                Name: station.Name,
                PriceEur: price,
                Address: FormatAddress(station.Location),
                DistanceKm: origin is null ? null : Math.Round(station.DistanceKm, 1),
                Open: station.IsOpen,
                MapUrl: MapUrl(station.Location, origin));
            (price is null ? withoutPrice : withPrice).Add(summary);
        }

        var now = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), Vienna);
        return new StationSearchResult(
            SearchArea: searchArea,
            FuelType: fuelType.ToDisplayName(),
            RetrievedAt: now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " (Austrian time)",
            Stations: withPrice,
            OtherStationsWithoutPrice: withoutPrice.Count > 0 ? withoutPrice : null,
            Note: stations.Count == 0 ? "No stations found." : regionNote is null ? PriceNote : regionNote + " " + PriceNote,
            Source: Source);
    }

    private static string? FormatAddress(StationLocation? location)
    {
        if (location is null)
        {
            return null;
        }

        var city = string.Join(' ', new[] { location.PostalCode, location.City }.Where(s => !string.IsNullOrWhiteSpace(s)));
        return string.Join(", ", new[] { location.Address?.Trim(), city }.Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    internal static string? MapUrl(StationLocation? location, GeocodingResult? origin)
    {
        if (location is null || (location.Latitude == 0 && location.Longitude == 0))
        {
            return null;
        }

        return origin is null
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"https://www.openstreetmap.org/?mlat={location.Latitude}&mlon={location.Longitude}#map=17/{location.Latitude}/{location.Longitude}")
            : string.Create(
                CultureInfo.InvariantCulture,
                $"https://www.openstreetmap.org/directions?engine=fossgis_osrm_car&route={origin.Latitude}%2C{origin.Longitude}%3B{location.Latitude}%2C{location.Longitude}");
    }

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

    internal sealed record StationSearchResult(
        string SearchArea,
        string FuelType,
        string RetrievedAt,
        IReadOnlyList<StationSummary> Stations,
        IReadOnlyList<StationSummary>? OtherStationsWithoutPrice,
        string Note,
        string Source);

    internal sealed record StationSummary(
        string Name,
        decimal? PriceEur,
        string? Address,
        double? DistanceKm,
        bool Open,
        string? MapUrl);
}
