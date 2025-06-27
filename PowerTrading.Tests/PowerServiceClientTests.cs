using Microsoft.Extensions.Logging;
using PowerTrading.Infrastructure.Clients;
using PowerTrading.Reporting.IntraDayReport;
using PowerTrading.WindowsService;
using Services;
using Xunit.Abstractions; // Namespace for native PowerService types

namespace PowerTrading.Tests;

public class PowerServiceClientTests {
    private readonly Mock<ILogger<PowerServiceClient>> _mockLogger = new();
    private readonly ITestOutputHelper testOutput;

    public PowerServiceClientTests(ITestOutputHelper testOutput) {
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
        this.testOutput = testOutput;
    }
    [Fact]
    public async Task GetTradesAsync_Normal_Is_Random() {
        // Arrange
        Environment.SetEnvironmentVariable("SERVICE_MODE", "Normal");  // / Normal , Error, Test
        var powerService = new PowerService();
        using var cts = new CancellationTokenSource();
        var runId = Guid.Empty;
        var runTime = new DateTime(2025, 6, 17, 21, 34, 0);

        var client = new PowerServiceClient(powerService, _mockLogger.Object);

        // Act
        var powerTrades = await client.GetTradesAsync(runId, runTime, cts.Token);

        // Assert
        powerTrades.Should().NotBeNull();
        powerTrades.Should().NotBeEmpty();
        powerTrades.First().Periods[0].Volume.Should().NotBe(1);
    }

    [Fact]
    public async Task GetTradesAsync_Test_Is_Not_Random() {
        // Arrange
        Environment.SetEnvironmentVariable("SERVICE_MODE", "Test");  // / Normal , Error, Test
        var powerService = new PowerService();
        using var cts = new CancellationTokenSource();
        var runId = Guid.Empty;
        var runTime = new DateTime(2025, 6, 17, 21, 34, 0);

        var client = new PowerServiceClient(powerService, _mockLogger.Object);

        // Act
        var powerTrades = await client.GetTradesAsync(runId, runTime, cts.Token);

        // Assert
        powerTrades.Should().NotBeNull();
        powerTrades.Should().HaveCount(2);
        powerTrades.First().Periods[0].Volume.Should().Be(1);

        // CleanUp
        Environment.SetEnvironmentVariable("SERVICE_MODE", "Normal");
    }

    [Fact]
    public async Task GetTradesAsync_Error_Is_Not_Random() {
        // Arrange
        Environment.SetEnvironmentVariable("SERVICE_MODE", "Error");  // / Normal , Error, Test
        var powerService = new PowerService();
        using var cts = new CancellationTokenSource();
        var runId = Guid.Empty;
        var runTime = new DateTime(2025, 6, 17, 21, 34, 0);

        var client = new PowerServiceClient(powerService, _mockLogger.Object);

        // Act
        await Assert.ThrowsAsync<PowerServiceException>(() =>
            client.GetTradesAsync(runId, runTime, cts.Token));

        // CleanUp
        Environment.SetEnvironmentVariable("SERVICE_MODE", "Normal");
    }


    [Fact]
    public async Task GetTradesAsync_ReturnsMappedTrades_WhenNotCancelled() {
        // Arrange
        var runId = Guid.Empty;
        var runTime = new DateTime(2025, 6, 17, 21, 34, 0);


        Services.PowerTrade[] nativePowerTrades = GetNateivePoverTrades(runTime);

        var mockPowerService = new Mock<IPowerService>();
        mockPowerService
            .Setup(s => s.GetTradesAsync(runTime.Date))
            .ReturnsAsync(nativePowerTrades);

        var client = new PowerServiceClient(mockPowerService.Object, _mockLogger.Object);
        using var cts = new CancellationTokenSource();

        // Act
        var powerTrades = await client.GetTradesAsync(runId, runTime, cts.Token);

        // Assert
        powerTrades.Should().NotBeNull();
        powerTrades.Should().HaveCount(1);
        var powerTrade = powerTrades.First();
        powerTrade.Date.Should().Be(runTime.Date);
        powerTrade.Periods.Should().HaveCount(2);
        powerTrade.Periods[0].Period.Should().Be(1);
        powerTrade.Periods[0].Volume.Should().Be(10);
        powerTrade.Periods[1].Period.Should().Be(2);
        powerTrade.Periods[1].Volume.Should().Be(20);
    }





    [Fact]
    public async Task GetTradesAsync_ThrowsOperationCanceledException_WhenCancelled() {
        // Arrange
        var runId = Guid.Empty;
        var runTime = new DateTime(2025, 6, 17, 21, 34, 0);


        Services.PowerTrade[] nativePowerTrades = GetNateivePoverTrades(runTime);


        var tcs = new TaskCompletionSource<IEnumerable<PowerTrade>>();
        var mockPowerService = new Mock<IPowerService>();
        mockPowerService
              .Setup(s => s.GetTradesAsync(runTime.Date))
              .ReturnsAsync(nativePowerTrades);


        var client = new PowerServiceClient(mockPowerService.Object, _mockLogger.Object);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await client.Invoking(c => c.GetTradesAsync(runId, runTime, cts.Token))
                    .Should()
                    .ThrowAsync<OperationCanceledException>();
    }

    private static Services.PowerTrade[] GetNateivePoverTrades(DateTime now) {
        var nativePowerTrade = Services.PowerTrade.Create(now.Date, 2);
        nativePowerTrade.Periods[0].Volume = 10;
        nativePowerTrade.Periods[1].Volume = 20;
        var nativePowerTrades = new[] { nativePowerTrade };
        return nativePowerTrades;
    }


}
