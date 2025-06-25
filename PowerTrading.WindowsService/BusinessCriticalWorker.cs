using System;
using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using PowerTrading.Reporting.IntraDayReport;

namespace PowerTrading.WindowsService {
    /// <summary>
    /// Base class for a background worker that schedules and processes run plans without overlapping executions.
    /// </summary>
    public abstract class BusinessCriticalWorker<TWorker> : BackgroundService {
        protected readonly ILogger<TWorker> _logger;
        private readonly WorkerSettings _settings;
        private readonly ITime _time;

        private readonly ConcurrentQueue<Tuple<Guid, DateTime>> _runPlans = new();

        // Semaphore to serialize starting the processing task.
        private readonly SemaphoreSlim _processingStartLock = new(1, 1);

        // Polly retry policy to acquire semaphore with retries and delays.
        private readonly AsyncRetryPolicy<bool> _processingStartLockRetryPolicy;

        private Task? _processingTask;
        public Task? ProcessingTask => _processingTask;

        public BusinessCriticalWorker(ILogger<TWorker> logger, IOptions<WorkerSettings> options, ITime time) {
            _logger = logger;
            _settings = options.Value;
            _time = time;

            _processingStartLockRetryPolicy = Policy
                .HandleResult<bool>(acquired => !acquired)
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: attempt => TimeSpan.FromSeconds(2),
                    onRetry: (result, timespan, retryCount, context) => {
                        _logger.LogWarning("Attempt {RetryCount} to acquire processing start lock failed, retrying in {Delay}.",
                            retryCount, timespan);
                    });
        }

        public override async Task StartAsync(CancellationToken cancellationToken) {
            _logger.LogInformation("Service starting...");
            await base.StartAsync(cancellationToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken) {
            _logger.LogInformation("Service stopping...");
            if (_processingTask != null) {
                try {
                    await _processingTask.WaitAsync(cancellationToken);
                } catch (OperationCanceledException) {
                    _logger.LogInformation("Processing task cancelled during shutdown.");
                }
            }
            await base.StopAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken token) {
            _logger.LogInformation("Execution loop starting...");
            Guid runId = default;
            DateTime runTime = default;

            try {

                while (!token.IsCancellationRequested) {
                    (runId, runTime) = EnqueueNewRunPlan();

                    // Trigger processing task asynchronously if not already running.
                    bool lockAcquired = false;

                    try {
                        lockAcquired = await _processingStartLockRetryPolicy.ExecuteAsync(async ct =>
                                       await _processingStartLock.WaitAsync(1 * 1000, ct), token);

                        if (!lockAcquired) {
                            _logger.LogWarning("Failed to acquire execution lock after retries. Skipping processing start.");
                            return;
                        }

                        _logger.LogInformation("Execution lock acquired.");

                        if (_processingTask != null && !_processingTask.IsCompleted) {
                            _logger.LogWarning("Processing task already running. Skipping new start.");
                            return;
                        }

                        _logger.LogInformation("Execution task is idle.");

                        if (token.IsCancellationRequested) {
                            _logger.LogWarning("Cancellation requested before starting processing task.");
                            return;
                        }

                        _logger.LogInformation("CancellationToken is idle.");

                        _processingTask = ProcessRunPlans(token);

                    } finally {
                        if (lockAcquired) {
                            _processingStartLock.Release();
                            _logger.LogDebug("Released execution lock.");
                        }
                    }

                    var interval = TimeSpan.FromMinutes(_settings.ExtractIntervalMinutes);
                    await Task.Delay(interval, token);
                }
            } catch (OperationCanceledException) {
                _logger.LogInformation("Execution loop cancelled. RunTime: {RunTime} RunId: {RunId}", runTime, runId);
            } catch (Exception ex) {
                _logger.LogError(ex, "Unexpected error in execution loop. RunTime: {RunTime} RunId: {RunId}", runTime, runId);
                throw;
            } finally {
                _logger.LogInformation("Execution loop stopping. RunTime: {RunTime} RunId: {RunId}", runTime, runId);
            }
        }

        /// <summary>
        /// Enqueues a new run plan with unique RunId and current run time.
        /// Logs backlog size if exceeding threshold.
        /// </summary>
        private (Guid, DateTime) EnqueueNewRunPlan() {
            var runId = Guid.NewGuid();
            var runTime = _time.GetTime();
            _runPlans.Enqueue(Tuple.Create(runId, runTime));

            if (_runPlans.Count > 10) // Adjust threshold as needed
            {
                _logger.LogWarning("Backlog size is high: {BacklogCount} scheduled runs pending processing.", _runPlans.Count);
            }

            _logger.LogInformation("Scheduled run enqueued for RunTime: {RunTime} RunId: {RunId}", runTime, runId);

            return (runId, runTime);
        }




        /// <summary>
        /// Processes all queued run plans, handling exceptions and respecting cancellation.
        /// </summary>
        private async Task ProcessRunPlans(CancellationToken token) {
            while (_runPlans.TryDequeue(out var schedule)) {
                token.ThrowIfCancellationRequested();

                var runId = schedule.Item1;
                var runTime = schedule.Item2;

                try {
                    _logger.LogInformation("Starting work for RunTime {RunTime} RunId: {RunId}", runTime, runId);

                    await WorkAsync(runId, runTime, token);

                    token.ThrowIfCancellationRequested();

                    _logger.LogInformation("Completed work for RunTime {RunTime} RunId: {RunId}", runTime, runId);
                } catch (OperationCanceledException) {
                    _logger.LogWarning("Work cancelled RunTime {RunTime} RunId: {RunId}", runTime, runId);
                    throw;
                } catch (Exception ex) {
                    _logger.LogError(ex, "Error processing scheduled RunTime {RunTime} RunId: {RunId}", runTime, runId);
                    // Continue processing despite errors.
                }
            }
        }

        /// <summary>
        /// Abstract method to be implemented for actual work processing.
        /// </summary>
        public abstract Task WorkAsync(Guid runId, DateTime runTime, CancellationToken token);

        public override void Dispose() {
            base.Dispose();
            _processingStartLock?.Dispose();
        }
    }
}
