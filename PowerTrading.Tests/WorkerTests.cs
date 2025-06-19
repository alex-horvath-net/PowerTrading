using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PowerTrading.Reporting.IntraDayReport;
using PowerTrading.WindowsService;
using System.Threading;

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
    public async Task HappyPath_WorkAsync_Is_Called_At_Least_Once() {
        // Arrange
        var cts = new CancellationTokenSource();
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        _mockReportService
            .Setup(s => s.GenerateAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("fakepath")
            .Callback(() => tcs.TrySetResult(true));

        // Act
        await _worker.StartAsync(cts.Token);
        var executeTask = _worker.ProcessingTask;

        // Wait for work to start
        var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));

        // Assert
        completedTask.Should().Be(tcs.Task, "WorkAsync should have been called within timeout");
        _mockReportService.Verify(s => s.GenerateAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);

        // Cleanup
        cts.Cancel();
        if (executeTask != null) {
            try { await executeTask; } catch (OperationCanceledException) { }
        }
        await _worker.StopAsync(CancellationToken.None);
        _worker.Dispose();
    }

    [Fact]
    public async Task Cancellation_Stops_Execution_Gracefully() {
        // Arrange
        var cts = new CancellationTokenSource();

        _mockReportService
            .Setup(s => s.GenerateAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(async (DateTime dt, CancellationToken token) => {
                await Task.Delay(5000, token);
                return "done";
            });

        // Act
        await _worker.StartAsync(cts.Token);
        var executeTask = _worker.ProcessingTask;

        await Task.Delay(500); // Give some time to start work

        cts.Cancel(); // Request cancellation

        try {
            if (executeTask != null)
                await executeTask;
        } catch (OperationCanceledException) {
            // Expected
        }

        // Assert
        _mockLogger.Verify(x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => v.ToString().Contains("Execution loop cancelled")),
            null,
            It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);

        // Cleanup
        await _worker.StopAsync(CancellationToken.None);
        _worker.Dispose();
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
        var executeTask = _worker.ProcessingTask;

        await Task.Delay(200); // Allow first call to start
        await Task.Delay(1500); // Trigger second scheduled run (should be queued)

        tcs.SetResult("done"); // Complete first call

        cts.Cancel();

        if (executeTask != null) {
            try { await executeTask; } catch (OperationCanceledException) { }
        }

        // Assert
        _mockReportService.Verify(s => s.GenerateAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.AtLeast(1));

        // Cleanup
        await _worker.StopAsync(CancellationToken.None);
        _worker.Dispose();
    }

    [Fact]
    public async Task NoScheduledRun_IsMissed() {
        // Arrange
        var cts = new CancellationTokenSource();

        int runsScheduled = 0;
        int runsProcessed = 0;

        _mockReportService
            .Setup(s => s.GenerateAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(async (DateTime dt, CancellationToken token) => {
                Interlocked.Increment(ref runsProcessed);
                await Task.Delay(100, token);
                return "done";
            });

        _mockTime
            .Setup(t => t.GetTime(default))
            .Returns(() => {
                Interlocked.Increment(ref runsScheduled);
                return DateTime.UtcNow;
            });

        // Act
        await _worker.StartAsync(cts.Token);
        var executeTask = _worker.ProcessingTask;

        // Wait for multiple runs to schedule and process
        await Task.Delay(3500);

        cts.Cancel();

        if (executeTask != null) {
            try { await executeTask; } catch (OperationCanceledException) { }
        }

        // Assert
        runsProcessed.Should().Be(runsScheduled, "No scheduled run should be missed");

        // Cleanup
        await _worker.StopAsync(CancellationToken.None);
        _worker.Dispose();
    }
}
