using PowerTrading.Domain;

namespace PowerTrading.Reporting.IntraDayReport;

public interface IIntraDayReportService {
    Task<string> GenerateAsync(Guid runId, DateTime runTime, CancellationToken token);
}

public class IntraDayReportService : IIntraDayReportService {
    private readonly ICsvExporter _csvExporter;
    private readonly IPowerServiceClient _powerServiceClient;
    private readonly PowerPositionAggregator _aggregator;

    public IntraDayReportService(
        IPowerServiceClient powerServiceClient,
        ICsvExporter csvExporter    ) {
        _csvExporter = csvExporter;
        _powerServiceClient = powerServiceClient;
        _aggregator = new PowerPositionAggregator();
    }

    public async Task<string> GenerateAsync(Guid runId, DateTime runTime, CancellationToken token) {

        // Extract
         var powerTrades = await _powerServiceClient.GetTradesAsync(runId, runTime, token);

        // Transform
        var powerPositions = _aggregator.AggregateByHour(powerTrades, runId, runTime);

        // Load
        var intraDayReportPath = await _csvExporter.Export(powerPositions, runId, runTime, token);

        return intraDayReportPath;
    }
}
