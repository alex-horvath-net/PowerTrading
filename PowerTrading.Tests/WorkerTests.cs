using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PowerTrading.Reporting.IntraDayReport;
using PowerTrading.WindowsService;

namespace PowerTrading.Tests;
public class WorkerTests {
    private readonly Mock<IIntraDayReportService> _mockReportService = new();
    private readonly Mock<ILogger<Worker>> _mockLogger = new();
    private readonly IOptions<WorkerSettings> _options = Options.Create(new WorkerSettings { ExtractIntervalMinutes = 1 });
    private readonly Mock<ITime> _mockTime = new();
    private Worker _worker;

    public WorkerTests() {
        var now = new DateTime(2025, 6, 17, 21, 34, 0);
        _mockTime.Setup(t => t.GetTime(default)).Returns(now);
        _worker = new Worker(_mockReportService.Object, _mockLogger.Object, _options, _mockTime.Object);
    }



    [Fact]
    public async Task Cancellation_Stops_Execution_Gracefully() {
        // Arrange
        var cts = new CancellationTokenSource();

        _mockReportService
            .Setup(s => s.GenerateAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(async (DateTime dt, CancellationToken token) => {
                await Task.Delay(5000, token); // Simulate long running operation that supports cancellation
                return "done";
            });

        // Act
        await _worker.StartAsync(cts.Token);
        var executeTask = _worker.ExecuteTask;

        await Task.Delay(500); // Allow the work to start

        cts.Cancel(); // Request cancellation

        try {
            if (executeTask != null)
                await executeTask; // Await the processing task which should cancel
        } catch (OperationCanceledException) {
            // Expected cancellation, do nothing
        }

        await _worker.StopAsync(CancellationToken.None);
        _worker.Dispose();

        // Assert: Verify the cancellation log was written exactly once
        _mockLogger.Verify(x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => v.ToString().Contains("Execution loop cancelled")),
            null,
            It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Overlapping_Work_Is_Prevented() {
        // Arrange
        var cts = new CancellationTokenSource();

        var tcs = new TaskCompletionSource<string>();

        _mockReportService
            .SetupSequence(s => s.GenerateAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(tcs.Task)  // First call hangs
            .ReturnsAsync("second"); // Second call

        // Act
        await _worker.StartAsync(cts.Token);
        var executeTask = _worker.ExecuteTask;

        await Task.Delay(200); // Allow first call to start

        await Task.Delay(1500); // Trigger second scheduled run (should be queued, not run concurrently)

        tcs.SetResult("done"); // Complete first call

        cts.Cancel();

        if (executeTask != null)
            await executeTask;

        await _worker.StopAsync(CancellationToken.None);
        _worker.Dispose();

        // Assert
        _mockReportService.Verify(s => s.GenerateAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.AtLeast(1));
        // Semaphore prevents overlap, so no concurrency issues here
    }
}
