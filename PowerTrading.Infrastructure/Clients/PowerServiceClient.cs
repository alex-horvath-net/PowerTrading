using Polly;
using Polly.Retry;
using PowerTrading.Reporting.IntraDayReport;
using Services;

namespace PowerTrading.Infrastructure.Clients;

/// <summary>
/// Encapsulates consumption logic of PowerService.dll and all of its types.
/// </summary>
public class PowerServiceClient : IPowerServiceClient {
    private readonly IPowerService _nativePowerService;
    private readonly AsyncRetryPolicy<IEnumerable<PowerTrade>> _retryPolicy;

    public PowerServiceClient(IPowerService nativePowerService) {
        _nativePowerService = nativePowerService;

        // Define a retry policy: 3 retries with exponential backoff, ignoring cancellation exceptions
        _retryPolicy = Policy<IEnumerable<PowerTrade>>
            .Handle<Exception>(ex => !(ex is OperationCanceledException))
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (exception, timespan, retryCount, context) => {
                    // Optional: log retry here, e.g. using ILogger if you inject it
                    // _logger.LogWarning($"Retry {retryCount} after {timespan.TotalSeconds}s due to: {exception.Message}");
                });
    }

    public async Task<IEnumerable<Domain.PowerTrade>> GetTradesAsync(DateTime runTime, CancellationToken token) {
        var nativePowerTrades = await GetTradesIfNotCanceledAsync(runTime, token);
        var domainPowerTrades = nativePowerTrades.Select(MapPowerTrade).ToArray();
        return domainPowerTrades; // PowerServiceClient doesn't leak any PowerService.dll type
    }

    private async Task<IEnumerable<PowerTrade>> GetTradesIfNotCanceledAsync(DateTime runTime, CancellationToken token) {
        token.ThrowIfCancellationRequested();

        // Run the native GetTradesAsync inside the retry policy
        var nativePowerTrades = await _retryPolicy.ExecuteAsync(async () => {
            var getTradesTask = _nativePowerService.GetTradesAsync(runTime.Date);
            var cancelableTask = Task.Delay(Timeout.Infinite, token);

            var completedTask = await Task.WhenAny(getTradesTask, cancelableTask);
            if (completedTask == cancelableTask)
                throw new OperationCanceledException(token);

            return await getTradesTask;
        });

        return nativePowerTrades;
    }

    private static Domain.PowerTrade MapPowerTrade(Services.PowerTrade nativePowerTrade) => new Domain.PowerTrade {
        Date = nativePowerTrade.Date,
        Periods = nativePowerTrade.Periods.Select(MapPowerPeriod).ToArray(),
    };

    private static Domain.PowerPeriod MapPowerPeriod(Services.PowerPeriod nativePowerPeriod) => new Domain.PowerPeriod() {
        Period = nativePowerPeriod.Period,
        Volume = nativePowerPeriod.Volume
    };
}
