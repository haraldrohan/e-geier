namespace EGeier.Sprit;

/// <summary>Client for the public Spritpreisrechner API of E-Control Austria.</summary>
public interface ISpritClient
{
    /// <summary>
    /// Searches gas stations around the given coordinates, cheapest first. The API returns up to
    /// ten stations; only the cheapest ones carry prices.
    /// </summary>
    Task<IReadOnlyList<GasStation>> SearchByLocationAsync(
        double latitude,
        double longitude,
        FuelType fuelType,
        bool includeClosed = false,
        CancellationToken cancellationToken = default);

    /// <summary>Searches the cheapest gas stations in a Bundesland or Bezirk.</summary>
    Task<IReadOnlyList<GasStation>> SearchByRegionAsync(
        long regionCode,
        RegionType regionType,
        FuelType fuelType,
        bool includeClosed = false,
        CancellationToken cancellationToken = default);

    /// <summary>Returns all Bundesländer with their Bezirke.</summary>
    /// <param name="includeCities">Also return the municipalities of each Bezirk.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<Region>> GetRegionsAsync(bool includeCities = false, CancellationToken cancellationToken = default);

    /// <summary>Returns all administrative units (states, districts, municipalities) with coordinates.</summary>
    Task<IReadOnlyList<AdministrativeState>> GetAdministrativeUnitsAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the API's welcome message, useful as a health check.</summary>
    Task<string> PingAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the status of the API's last data synchronisation as plain text.</summary>
    Task<string> GetMonitoringAsync(CancellationToken cancellationToken = default);
}
