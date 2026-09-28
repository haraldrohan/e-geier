using EGeier.Sprit;
using Microsoft.Extensions.DependencyInjection;

namespace EGeier.Mcp;

/// <summary>Loads the region list (with municipalities) once and keeps it for a day.</summary>
internal sealed class RegionDirectory(IServiceScopeFactory scopeFactory, TimeProvider timeProvider) : IDisposable
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<Region>? _regions;
    private DateTimeOffset _loadedAt;

    public async Task<IReadOnlyList<Region>> GetRegionsAsync(CancellationToken cancellationToken)
    {
        if (_regions is { } cached && timeProvider.GetUtcNow() - _loadedAt < Lifetime)
        {
            return cached;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_regions is null || timeProvider.GetUtcNow() - _loadedAt >= Lifetime)
            {
                // ISpritClient is a transient typed client; resolve it per load instead of capturing it.
                using var scope = scopeFactory.CreateScope();
                var client = scope.ServiceProvider.GetRequiredService<ISpritClient>();
                _regions = await client.GetRegionsAsync(includeCities: true, cancellationToken).ConfigureAwait(false);
                _loadedAt = timeProvider.GetUtcNow();
            }

            return _regions;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();
}
