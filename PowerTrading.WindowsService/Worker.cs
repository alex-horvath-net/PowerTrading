using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using PowerTrading.Reporting.IntraDayReport;
using PowerTrading.WindowsService;

public class Worker : BackgroundService {
    private readonly ILogger<Worker> _logger;
    private readonly IIntraDayReportService _reportService;
    private readonly ITime _timeProvider;
    private readonly WorkerSettings _settings;
    public ConcurrentQueue<(Guid runId, DateTime runTime)> RunPlans { get; private set; } = new();
    private readonly SemaphoreSlim _processingLock = new(1, 1);
    public Task ProcessingTask { get; private set; } = Task.CompletedTask;

    public Worker(ILogger<Worker> logger, IIntraDayReportService reportService, ITime timeProvider, IOptions<WorkerSettings> options) {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _reportService = reportService ?? throw new ArgumentNullException(nameof(reportService));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _settings = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

  
    protected override async Task ExecuteAsync(CancellationToken token) {
        _logger.LogInformation("Execution started.");

        while (!token.IsCancellationRequested) {
            var runId = Guid.NewGuid();
            var runTime = _timeProvider.GetTime();

            RunPlans.Enqueue((runId, runTime));
            _logger.LogInformation("New report plan is placed. RunId: {RunId} at {RunTime}, ThreadId: {ThreadId}", runId, runTime, Environment.CurrentManagedThreadId);

            try {
                if (await _processingLock.WaitAsync(0, token)) {
                    _logger.LogInformation("New lock acquiered. RunId: {RunId} at {RunTime}, ThreadId: {ThreadId}", runId, runTime, Environment.CurrentManagedThreadId);
                    ProcessingTask = Task.Run(() => ProcessingAsync(token));
                    _logger.LogInformation("Processig is fired and forgot. {RunId} at {RunTime}, ThreadId: {ThreadId}", runId, runTime, Environment.CurrentManagedThreadId);

                } else {
                    _logger.LogInformation("Current execution is skiped, beacase of overlapping execution. RunId: {RunId}", runId);
                }
            } catch (OperationCanceledException) {
                _logger.LogInformation("Execution cancelled. RunId: {RunId}", runId);
                break;
            } catch (Exception ex) {
                _logger.LogError(ex, "Execution failed. RunId: {RunId}, Exception: {Exception}", runId, ex.Message);
                // Depending on severity, consider whether to break loop or continue
            }

            try {
                token.ThrowIfCancellationRequested();
                await Task.Delay(TimeSpan.FromMinutes(_settings.ExtractIntervalMinutes), token);
                token.ThrowIfCancellationRequested();
            } catch (OperationCanceledException) {
                _logger.LogInformation("Delay cancelled, stopping execution loop.");
                break;
            }
        }

        _logger.LogInformation("Execution stopped.");
    }

    private async Task ProcessingAsync(CancellationToken token) {
        try {
            while (RunPlans.TryDequeue(out var run)) {
                token.ThrowIfCancellationRequested();
                _logger.LogInformation("New processing loop started. RunId; {RunId}, RunTime: {RunTime}, ThreadId: {ThreadId}", run.runId, run.runTime, Environment.CurrentManagedThreadId);

                try {
                    
                    var reportPath = await _reportService.GenerateAsync(run.runId, run.runTime, token);
                    _logger.LogInformation("New report is generated. Path {Path}, RunId: {RunId}", reportPath, run.runId);
                    
                    token.ThrowIfCancellationRequested();
                } catch (OperationCanceledException) {
                    _logger.LogInformation("Report generation is canceled. RunId {RunId}", run.runId);
                    RunPlans.Enqueue(run);  // Re-enqueue to ensure no missed runs
                    throw;
                } catch (Exception ex) {
                    _logger.LogError(ex, "Error during generating new report. runId {RunId}. Continuing with next planned report if any.", run.runId);
                    // Do NOT rethrow: continue with next run
                }
            }
        } catch (OperationCanceledException) {
            _logger.LogInformation("Processing loop cancelled.");
        } catch (Exception ex) {
            _logger.LogError(ex, "Processing loop failed.");
        } finally {
            _processingLock.Release();
            _logger.LogDebug("Processing lock released.");
            _logger.LogInformation("Processing loop finalized.");
        }
    }

    public override void Dispose() {
        _processingLock?.Dispose();
        _logger.LogInformation("Service disposing...");
        base.Dispose();
    }

    public override Task StartAsync(CancellationToken cancellationToken) {
        _logger.LogInformation("Service starting...");
        return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken) {
        _logger.LogInformation("Service stopping...");
        return base.StopAsync(cancellationToken);
    }
}

