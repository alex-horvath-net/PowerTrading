using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PowerTrading.Reporting.IntraDayReport;
using PowerTrading.WindowsService;
using Xunit.Abstractions;

namespace PowerTrading.Tests;

public class WorkerTests {
    private readonly Mock<IIntraDayReportService> _mockReportService = new();
    private readonly Mock<ILogger<Worker>> _mockLogger = new();
    private readonly IOptions<WorkerSettings> _options = Options.Create(new WorkerSettings { ExtractIntervalMinutes = 1.0 / 60 });
    private readonly Mock<ITime> _mockTime = new();
    private readonly ITestOutputHelper _testOutput;
    CancellationTokenSource _cts = new CancellationTokenSource();

    public WorkerTests(ITestOutputHelper testOutput) {
        this._testOutput = testOutput;

        var expectedTime = new DateTime(2025, 6, 17, 21, 34, 0);
        _mockTime
            .Setup(t => t.GetTime(It.IsAny<DateTime?>()))
            .Returns(expectedTime);

        var expectePath = "path/to/report.csv";
        _mockReportService
            .Setup(s => s.GenerateAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectePath);

        _mockLogger
            .Setup(l => l.Log(
                    It.IsAny<LogLevel>(),
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()))
            .Callback<LogLevel, EventId, object, Exception, Delegate>((level, eventId, state, exception, formatter) => {
                var message = formatter.DynamicInvoke(state, exception) as string;
                testOutput.WriteLine(message);
            });


    }

    [Fact]
    public async Task Succefully_Generated_Report() {
        // Arrange
        var worker = new Worker(_mockLogger.Object, _mockReportService.Object, _mockTime.Object, _options);

        // Act
        await worker.StartAsync(_cts.Token);
        //await worker.ProcessingTask;

        // Assert
        _mockReportService.Verify(s => s.GenerateAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);

        // Cleanup
        _testOutput.WriteLine("Cleanup...");
        await worker.StopAsync(_cts.Token);
        worker.Dispose();
    }



    [Fact]
    public async Task Cancelled_Report_Generation() {
        // Arrange
        var expectedPath = "path/to/report.csv";
        _mockReportService
            .Setup(s => s.GenerateAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
             .Returns(async (Guid _, DateTime _, CancellationToken token) => {
                 await Task.Delay(3000, token); // Simulate long-running task
                 return expectedPath;
             });

        var worker = new Worker(_mockLogger.Object, _mockReportService.Object, _mockTime.Object, _options);

        // Act
        await worker.StartAsync(_cts.Token);
        await Task.Delay(500); // Allow some time for the worker to start processing
        _cts.Cancel(); 
       

        // Assert
        _mockReportService.Verify(s => s.GenerateAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);

        // Cleanup
        _testOutput.WriteLine("Cleanup...");
        await worker.StopAsync(_cts.Token);
        worker.Dispose();
    }

    [Fact]
    public async Task Overlapping_Report_Generation_is_Prevented() {
        // Arrange
        var expectedPath = "path/to/report.csv";
        _mockReportService
            .Setup(s => s.GenerateAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
             .Returns(async (Guid _, DateTime _, CancellationToken token) => {
                 await Task.Delay(3000, token); // Simulate long-running task
                 return expectedPath;
             });

        var worker = new Worker(_mockLogger.Object, _mockReportService.Object, _mockTime.Object, _options);

        // Act
        await worker.StartAsync(_cts.Token);
        _testOutput.WriteLine("Wait enough time for multiple scheduled runs...");
        await Task.Delay(4000); // Wait enough time for multiple scheduled runs (1 per sec)


        // Assert
        worker.RunPlans.Count.Should().BeGreaterThan(1);

        // Cleanup
        _testOutput.WriteLine("Cleanup...");
        await worker.StopAsync(_cts.Token);
        worker.Dispose();
    }


    [Fact]
    public async Task NoScheduledRun_IsMissed() {
        // Arrange
        int runsProcessed = 0;
        var expectedPath = "path/to/report.csv";
        _mockReportService
            .SetupSequence(s => s.GenerateAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(async () => {
                 Interlocked.Increment(ref runsProcessed);
                 await Task.Delay(3000,_cts.Token); // Simulate long-running task
                 return expectedPath;
             })
            .Returns( () => {
                 Interlocked.Increment(ref runsProcessed);
                 return Task.FromResult( expectedPath);
             });

        var worker = new Worker(_mockLogger.Object, _mockReportService.Object, _mockTime.Object, _options);

        // Act
        await worker.StartAsync(_cts.Token);
        _testOutput.WriteLine("Wait enough time for multiple scheduled runs...");
        await Task.Delay(4000); // Wait enough time for multiple scheduled runs (1 per sec)
        await worker.ProcessingTask; // Ensure processing is complete

        // Assert
        runsProcessed.Should().Be(2);

        // Cleanup
        _testOutput.WriteLine("Cleanup...");
        await worker.StopAsync(_cts.Token);
        worker.Dispose();
    }




    private void AssertLog(string logMessage) {
        _mockLogger.Verify(x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString().Contains(logMessage)),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                    Times.AtLeastOnce);
    }
}
