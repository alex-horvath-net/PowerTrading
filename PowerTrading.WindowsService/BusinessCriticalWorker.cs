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

        private readonly ConcurrentQueue<Tuple<Guid, DateTime>> _RunPlans = new();
        private readonly SemaphoreSlim _processingStartLock = new(1, 1);

        private Task? _processRunPlansTask;
        public Task? ProcessingTask => _processRunPlansTask;
        public BusinessCriticalWorker(ILogger<TWorker> logger, IOptions<WorkerSettings> options, ITime time) {
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
            if (_processRunPlansTask != null) {
                try {
                    await _processRunPlansTask.WaitAsync(cancellationToken);
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
                var interval = TimeSpan.FromMinutes(_settings.ExtractIntervalMinutes);

                while (!token.IsCancellationRequested) {
                    (runId, runTime) = PlaceANewRunPlan();

                    await FireAndForgetProcessRunPlansTaskWithoutOverlapping(token);
                    // this comment line might run on new thread, and hits almost imadiatelly,
                    // because the internal _processRunPlansTask is started, but not waited.

                    await Task.Delay(interval, token);
                    // _processRunPlansTask might still running in the background, 
                    // so the next loop potentially could overlap with this current loop.
                }
            } catch (OperationCanceledException) {
                _logger.LogInformation("Execution loop cancelled  RunTime: {RunTime} RunId: {RunId} ", runTime, runId);
            } catch (Exception ex) {
                _logger.LogError(ex, "Unexpected error in execution loop RunTime: {RunTime} RunId: {RunId} ", runTime, runId);
                throw;
            } finally {
                _logger.LogInformation("Execution loop stopping. RunTime: {RunTime} RunId: {RunId} ", runTime, runId);
            }
        }

        private (Guid, DateTime) PlaceANewRunPlan() {
            var runId = Guid.NewGuid();
            var runTime = _time.GetTime();
            _RunPlans.Enqueue(Tuple.Create(runId, runTime)); // mutiple threads can enqueue safely
            if (_RunPlans.Count > 10) { // example threshold, adjust as needed
                _logger.LogWarning("Backlog size is high: {BacklogCount} scheduled runs pending processing.", _RunPlans.Count);
            }
            _logger.LogInformation("Scheduled run enqueued for RunTime: {RunTime} RunId: {RunId} ", runTime, runId);


            return (runId, runTime);
        }

        private async Task FireAndForgetProcessRunPlansTaskWithoutOverlapping(CancellationToken token) {
            var isStarted = false;

            try {
                _logger.LogDebug("Waiting to enter processing start lock...");
                isStarted = await _processingStartLock.WaitAsync(60 * 60 * 1000, token);
                _logger.LogDebug("Entered processing start lock.");
                if (!isStarted) {
                    _logger.LogWarning("Skipping processing due overlapping execution.");
                    return; // Skip processing if already running
                }

                var isRuning = _processRunPlansTask != null && !_processRunPlansTask.IsCompleted;
                if (isRuning) {
                    _logger.LogWarning("Skipping processing due processing is still running.");
                    return; // Skip processing if already running
                }

                if (token.IsCancellationRequested) {
                    _logger.LogInformation("Cancellation requested before starting processing task.");
                    return;
                }

                // fier and forget the processing task
                // this approach allows the service to continue scheduling new runs without waiting for processing to complete,
                _processRunPlansTask = ProcessRunPlans(token).ContinueWith(t =>
                {
                    if (t.IsFaulted) {
                        _logger.LogError(t.Exception, "Processing task faulted");
                    }
                }, TaskContinuationOptions.OnlyOnFaulted);


            } finally {
                // Release the semaphore only if we acquired it
                // This ensures we don't leave it locked if we never entered the processing
                // loop due to overlapping execution.

                if (isStarted) {
                    _processingStartLock.Release();
                    _logger.LogDebug("Released processing start lock.");
                }
            }
        }

        private async Task ProcessRunPlans(CancellationToken token) {

            while (_RunPlans.TryDequeue(out var schedule)) {
                token.ThrowIfCancellationRequested();
                var runId = schedule.Item1;
                var runTime = schedule.Item2;
                try {
                    _logger.LogInformation("Starting work for RunTime {RunTime} RunId: {RunId} ", runTime, runId);

                    await WorkAsync(runId, runTime, token);
                    token.ThrowIfCancellationRequested();

                    _logger.LogInformation("Completed work for RunTime {RunTime} RunId: {RunId} ", runTime, runId);
                } catch (OperationCanceledException) {
                    _logger.LogWarning("Work cancelled.RunTime {RunTime} RunId: {RunId} ", runTime, runId);
                    throw; // Propagate cancellation
                } catch (Exception ex) {
                    _logger.LogError(ex, "Error processing scheduled RunTime {RunTime} RunId: {RunId} ", runTime, runId);
                    // Swallow other exceptions so loop continues
                }
            }
        }
        public abstract Task WorkAsync(Guid runId, DateTime runTime, CancellationToken token);

        public override void Dispose() {
            base.Dispose();
            _processingStartLock?.Dispose();
        }
    }
}
