using EGeier.Geocoding;
using EGeier.Sprit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EGeier;

/// <summary>Dependency injection registration for E-Geier.</summary>
public static class ServiceCollectionExtensions
{
    private static readonly string UserAgent =
        $"E-Geier/{typeof(ServiceCollectionExtensions).Assembly.GetName().Version?.ToString(3)} (+https://github.com/haraldrohan/e-geier)";

    /// <summary>Registers <see cref="ISpritClient"/> as a typed HTTP client.</summary>
    /// <returns>The HTTP client builder, e.g. to add resilience handlers.</returns>
    public static IHttpClientBuilder AddSpritClient(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        return services.AddHttpClient<ISpritClient, SpritClient>(client =>
        {
            client.BaseAddress = SpritClient.DefaultBaseAddress;
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        });
    }

    /// <summary>
    /// Registers <see cref="NominatimGeocoder"/> as <see cref="IGeocoder"/>, including a memory cache.
    /// </summary>
    /// <returns>The HTTP client builder, e.g. to add resilience handlers.</returns>
    public static IHttpClientBuilder AddNominatimGeocoder(this IServiceCollection services, Action<NominatimOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddMemoryCache();
        var options = services.AddOptions<NominatimOptions>();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        return services.AddHttpClient<IGeocoder, NominatimGeocoder>(client => client.Timeout = TimeSpan.FromSeconds(20));
    }
}
