using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using PowerTrading.Reporting.IntraDayReport;

namespace PowerTrading.WindowsService {
    public abstract class BusinessCriticalWorker<TWorker> : BackgroundService {

        protected readonly ILogger<TWorker> _logger;
        private readonly WorkerSettings _settings;
        private readonly ITime _time;

        private readonly ConcurrentQueue<DateTime> _scheduledRuns = new();
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private readonly SemaphoreSlim _processingLock = new(1, 1);

        private Task? _processingTask;
        public Task? ProcessingTask => _processingTask;
        public BusinessCriticalWorker( 
            ILogger<TWorker> logger,
            IOptions<WorkerSettings> options,
            ITime time) {
            _logger = logger;
            _settings = options.Value;
            _time = time;
        }

        public override async Task StartAsync(CancellationToken cancellationToken) {
            _logger.LogInformation("Service starting...");
            await base.StartAsync(cancellationToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken) {
            _logger.LogInformation("Service stopping...");
            await base.StopAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken token) {
            _logger.LogInformation("Execution loop starting...");

            try {
                var interval = TimeSpan.FromMinutes(_settings.ExtractIntervalMinutes);

                while (!token.IsCancellationRequested) {
                    var scheduledRun = _time.GetTime();
                    _scheduledRuns.Enqueue(scheduledRun);
                    _logger.LogInformation($"Scheduled run enqueued for {scheduledRun:O}");

                    await StartProcessorIfNotRunningAsync(token);

                    await Task.Delay(interval, token);
                }
            } catch (OperationCanceledException) {
                _logger.LogInformation("Execution loop cancelled.");
            } catch (Exception ex) {
                _logger.LogError(ex, "Unexpected error in execution loop.");
                throw;
            } finally {
                _logger.LogInformation("Execution loop stopping.");
            }
        }

        private async Task StartProcessorIfNotRunningAsync(CancellationToken token) {
            await _processingLock.WaitAsync(token);
            try {
                if (_processingTask == null || _processingTask.IsCompleted) {
                    _processingTask = ProcessQueueAsync(token);
                }
            } finally {
                _processingLock.Release();
            }
        }

        private async Task ProcessQueueAsync(CancellationToken token) {
            if (!await _semaphore.WaitAsync(0, token)) {
                _logger.LogInformation("Processor already running, skipping invocation.");
                return;
            }

            try {
                while (_scheduledRuns.TryDequeue(out var scheduledRun)) {
                    token.ThrowIfCancellationRequested();

                    try {
                        _logger.LogInformation($"Starting work for scheduled run at {scheduledRun:O}");
                        await WorkAsync(scheduledRun, token);
                        _logger.LogInformation($"Completed work for scheduled run at {scheduledRun:O}");
                    } catch (OperationCanceledException) {
                        _logger.LogWarning("Work cancelled.");
                        throw; // Propagate cancellation
                    } catch (Exception ex) {
                        _logger.LogError(ex, $"Error processing scheduled run at {scheduledRun:O}");
                        // Swallow other exceptions so loop continues
                    }
                }
            } finally {
                _semaphore.Release();
            }
        }
        public abstract Task WorkAsync(DateTime scheduledRun, CancellationToken token);

        public override void Dispose() {
            base.Dispose();
            _processingLock.Dispose();
            _semaphore.Dispose();
        }
    }
}
