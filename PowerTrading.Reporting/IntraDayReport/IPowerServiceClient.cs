using PowerTrading.Domain;

namespace PowerTrading.Reporting.IntraDayReport;

public interface IPowerServiceClient {
    Task<IEnumerable<PowerTrade>> GetTradesAsync(Guid runId, DateTime runTime, CancellationToken token);
}
