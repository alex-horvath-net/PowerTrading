using Microsoft.Extensions.Options;
using PowerTrading.Reporting.IntraDayReport;

namespace PowerTrading.WindowsService {
    public class Worker : BusinessCriticalWorker<Worker>, IHostedService {
        private readonly IIntraDayReportService _intraDayReport;

        public Worker(IIntraDayReportService intraDayReport, ILogger<Worker> logger, IOptions<WorkerSettings> options, ITime time) : base(logger, options, time) {
            _intraDayReport = intraDayReport;
        }
        public override async Task WorkAsync(Guid runId, DateTime runTime, CancellationToken token) {
            _logger.LogInformation("Report generation is started for RunTime {RunTime} RunId: {RunId} ", runTime, runId);

            var reportPath = await _intraDayReport.GenerateAsync(runTime, token);
            
            _logger.LogInformation("Report generation completed. File saved to {reportPath}, RunTime {RunTime} RunId: {RunId} ", reportPath, runTime, runId);
        }
    }
}
