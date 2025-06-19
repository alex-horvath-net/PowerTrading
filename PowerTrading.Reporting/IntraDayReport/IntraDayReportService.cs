using PowerTrading.Domain;

namespace PowerTrading.Reporting.IntraDayReport;

public interface IIntraDayReportService {
    Task<string> GenerateAsync(DateTime scheduledRun, CancellationToken token);
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

    public async Task<string> GenerateAsync(DateTime scheduledRun, CancellationToken token) {

        // Extract
         var powerTrades = await _powerServiceClient.GetTradesAsync(scheduledRun, token);

        // Transform
        var powerPositions = _aggregator.AggregateByHour(powerTrades);

        // Load
        var intraDayReportPath = await _csvExporter.Export(powerPositions, token);

        return intraDayReportPath;
    }
}
