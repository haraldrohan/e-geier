using System.Diagnostics.CodeAnalysis;

namespace EGeier.Geocoding;

/// <summary>Serialises callers so that consecutive requests start at least a minimum interval apart.</summary>
[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Lives for the whole process; SemaphoreSlim only needs disposal when its wait handle is used.")]
internal sealed class RequestThrottle(TimeProvider timeProvider)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTimeOffset _lastRequest = DateTimeOffset.MinValue;

    /// <summary>Process-wide throttle, since the Nominatim limit applies per application, not per instance.</summary>
    public static RequestThrottle Shared { get; } = new(TimeProvider.System);

    /// <summary>Runs <paramref name="action"/> once the interval since the previous run has passed.</summary>
    public async Task<T> RunAsync<T>(TimeSpan minimumInterval, Func<Task<T>> action, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var wait = _lastRequest + minimumInterval - timeProvider.GetUtcNow();
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, timeProvider, cancellationToken).ConfigureAwait(false);
            }

            try
            {
                return await action().ConfigureAwait(false);
            }
            finally
            {
                _lastRequest = timeProvider.GetUtcNow();
            }
        }
        finally
        {
            _gate.Release();
        }
    }
}
