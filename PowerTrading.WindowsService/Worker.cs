using Microsoft.Extensions.Options;
using PowerTrading.Reporting.IntraDayReport;

namespace PowerTrading.WindowsService {
    public class Worker : BusinessCriticalWorker<Worker>, IHostedService {
        private readonly IIntraDayReportService _intraDayReport;

        public Worker(IIntraDayReportService intraDayReport, ILogger<Worker> logger, IOptions<WorkerSettings> options, ITime time) : base(logger, options, time) {
            _intraDayReport = intraDayReport;
        }
        public override async Task WorkAsync(DateTime scheduledRun, CancellationToken token) {
            _logger.LogInformation($"Report generation is started for {scheduledRun:O}");
            var reportPath = await _intraDayReport.GenerateAsync(scheduledRun, token);
            _logger.LogInformation($"Report generation completed. File saved to {reportPath}");
        }
    }
}
