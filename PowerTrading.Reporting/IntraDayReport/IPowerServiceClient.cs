using PowerTrading.Domain;

namespace PowerTrading.Reporting.IntraDayReport;

public interface IPowerServiceClient {
    Task<IEnumerable<PowerTrade>> GetTradesAsync(DateTime scheduledRun, CancellationToken token);
}
