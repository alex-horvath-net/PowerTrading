using PowerTrading.Reporting.IntraDayReport;
using Services;

namespace PowerTrading.Infrastructure.Clients;
/// <summary>
/// Encapsulates consumption logic of PowerService.dll and all of its types.
/// </summary>
public class PowerServiceClient : IPowerServiceClient {
    private readonly IPowerService _nativePowerService;

    public PowerServiceClient(IPowerService nativePowerService) {
        _nativePowerService = nativePowerService;
    }

    public async Task<IEnumerable<Domain.PowerTrade>> GetTradesAsync(DateTime runTime, CancellationToken token) {
        var nativePowerTrades = await GetTradesIfNotCanceledAsync(runTime,  token);
        var domainPowerTrades = nativePowerTrades.Select(MapPowerTrade).ToArray();
        return domainPowerTrades; // PowerServiceClient doesn't leak any PowerService.dll type
    }

    private  async Task<IEnumerable<PowerTrade>> GetTradesIfNotCanceledAsync(DateTime runTime, CancellationToken token) {
        //  GetTradesAsync has no CancellationToken parameter    
        var getTradesTask = _nativePowerService.GetTradesAsync(runTime.Date);
        var cancelableTask = Task.Delay(Timeout.Infinite, token); // create a cancelable task that will complete when the token is cancelled
        token.ThrowIfCancellationRequested();

        var completedTask = await Task.WhenAny(getTradesTask, cancelableTask);
        if (completedTask == cancelableTask)
            throw new OperationCanceledException(token);

        var nativePowerTrades = await getTradesTask; // completed successfully
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
