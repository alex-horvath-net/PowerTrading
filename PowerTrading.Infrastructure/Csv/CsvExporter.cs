using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using PowerTrading.Reporting.IntraDayReport;

namespace PowerTrading.Infrastructure.Csv;
public class CsvExporter : ICsvExporter {
    private readonly CsvExporterSettings _settings;
    private readonly ITime _time;
    private readonly ILogger<CsvExporter> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;

    public CsvExporter(ITime time, IOptions<CsvExporterSettings> options, ILogger<CsvExporter> logger) {
        _settings = options.Value;
        _time = time;
        _logger = logger;
        _retryPolicy = Policy
            .Handle<Exception>(ex => !(ex is OperationCanceledException))
            .WaitAndRetryAsync(
                3,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (exception, timespan, retryCount, context) => {
                    _logger.LogWarning("Retry {RetryCount} after {TotalSeconds}s due to: {ExceptionMessage}", retryCount, timespan.TotalSeconds, exception.Message);
                });
    }

    public async Task<string> Export(List<PowerTrading.Domain.PowerPosition> powerPositions, CancellationToken token) {
        token.ThrowIfCancellationRequested();

        var reportName = GenerateFileName(_time.GetTime());
        var reportPath = Path.Combine(_settings.OutputFolder, reportName);
        var reportContent = GetCsvContent(powerPositions);

        if (!Directory.Exists(_settings.OutputFolder)) {
            Directory.CreateDirectory(_settings.OutputFolder);
        }

        await _retryPolicy.ExecuteAsync(async ct => {
            await File.WriteAllTextAsync(reportPath, reportContent, Encoding.UTF8, ct);
        }, token);

        return reportPath;
    }

    public static string GenerateFileName(DateTime createdAt) {
        // Format date/time parts
        string datePart = createdAt.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        string timePart = createdAt.ToString("HHmm", CultureInfo.InvariantCulture);

        return $"PowerPosition_{datePart}_{timePart}.csv";
    }

    private string GetCsvContent(List<Domain.PowerPosition> powerPositions) {
        if (powerPositions == null)
            throw new ArgumentNullException(nameof(powerPositions));

        var reportContent = new StringBuilder();

        // Write CSV header
        reportContent.AppendLine($"Local Time{_settings.Separator}Volume");

        foreach (var powerPosition in powerPositions) {
            // Write CSV record
            var localTime = $"{powerPosition.Hour:D2}:00";
            var volume = powerPosition.Volume.ToString($"F{_settings.DecimalPlaces}", CultureInfo.InvariantCulture);  // decimal separator is a dot (.)
            reportContent.AppendLine($"{localTime}{_settings.Separator}{volume}");
        }

        return reportContent.ToString();
    }
}
