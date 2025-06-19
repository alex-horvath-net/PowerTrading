using System;
using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Options;
using PowerTrading.Reporting.IntraDayReport;

namespace PowerTrading.WindowsService {
    public abstract class BusinessCriticalWorker<TWorker> : BackgroundService {

        protected readonly ILogger<TWorker> _logger;
        private readonly WorkerSettings _settings;
        private readonly ITime _time;

        private readonly ConcurrentQueue<Tuple<Guid, DateTime>> _scheduledRuns = new();
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
            Guid runId = default;
            DateTime runTime = default;

            try {
                var interval = TimeSpan.FromMinutes(_settings.ExtractIntervalMinutes);

                while (!token.IsCancellationRequested) {
                    runId = Guid.NewGuid();
                    runTime = _time.GetTime();
                    _scheduledRuns.Enqueue(Tuple.Create(runId, runTime));
                    _logger.LogInformation("Scheduled run enqueued for RunTime: {RunTime} RunId: {RunId} ",  runTime, runId);

                    await StartProcessorIfNotRunningAsync(token);

                    await Task.Delay(interval, token);
                }
            } catch (OperationCanceledException) {
                _logger.LogInformation("Execution loop cancelled  RunTime: {RunTime} RunId: {RunId} ",  runTime, runId);
            } catch (Exception ex) {
                _logger.LogError(ex, "Unexpected error in execution loop RunTime: {RunTime} RunId: {RunId} ", runTime, runId);
                throw;
            } finally {
                _logger.LogInformation("Execution loop stopping. RunTime: {RunTime} RunId: {RunId} ", runTime, runId);
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
                while (_scheduledRuns.TryDequeue(out var schedule)) {
                    token.ThrowIfCancellationRequested();
                    var runId = schedule.Item1;
                    var runTime = schedule.Item2;
                    try {
                        _logger.LogInformation("Starting work for RunTime {RunTime} RunId: {RunId} ", runTime, runId);

                        await WorkAsync(runId, runTime, token);

                        _logger.LogInformation("Completed work for RunTime {RunTime} RunId: {RunId} ", runTime, runId);
                    } catch (OperationCanceledException) {
                        _logger.LogWarning("Work cancelled.");
                        throw; // Propagate cancellation
                    } catch (Exception ex) {
                        _logger.LogError(ex, "Error processing scheduled RunTime {RunTime} RunId: {RunId} ", runTime, runId);
                        // Swallow other exceptions so loop continues
                    }
                }
            } finally {
                _semaphore.Release();
            }
        }
        public abstract Task WorkAsync(Guid runId, DateTime runTime, CancellationToken token);

        public override void Dispose() {
            base.Dispose();
            _processingLock.Dispose();
            _semaphore.Dispose();
        }
    }
}
