using PowerTrading.Infrastructure.Clients;
using PowerTrading.Reporting.IntraDayReport;
using Services; // Namespace for native PowerService types

namespace PowerTrading.Tests;
public class PowerServiceClientTests {

    [Fact]
    public async Task GetTradesAsync_ReturnsMappedTrades_WhenNotCancelled() {
        // Arrange
        var now = new DateTime(2025, 6, 17);
        var mockTime = new Mock<ITime>();
        mockTime
            .Setup(t => t.GetTime(It.IsAny<DateTime?>()))
            .Returns(now);

        PowerTrade[] nativePowerTrades = GetNateivePoverTrades(now);

        var mockPowerService = new Mock<IPowerService>();
        mockPowerService
            .Setup(s => s.GetTradesAsync(now.Date))
            .ReturnsAsync(nativePowerTrades);

        var client = new PowerServiceClient( mockPowerService.Object);
        using var cts = new CancellationTokenSource();

        // Act
        var powerTrades = await client.GetTradesAsync(now, cts.Token);

        // Assert
        powerTrades.Should().NotBeNull();
        powerTrades.Should().HaveCount(1);
        var powerTrade = powerTrades.First();
        powerTrade.Date.Should().Be(now.Date);
        powerTrade.Periods.Should().HaveCount(2);
        powerTrade.Periods[0].Period.Should().Be(1);
        powerTrade.Periods[0].Volume.Should().Be(10);
        powerTrade.Periods[1].Period.Should().Be(2);
        powerTrade.Periods[1].Volume.Should().Be(20);
    }



    [Fact]
    public async Task GetTradesAsync_ThrowsOperationCanceledException_WhenCancelled() {
        // Arrange
        var now = new DateTime(2025, 6, 17);
        var mockTime = new Mock<ITime>();
        mockTime
            .Setup(t => t.GetTime(It.IsAny<DateTime?>()))
            .Returns(now);

        PowerTrade[] nativePowerTrades = GetNateivePoverTrades(now);


        var tcs = new TaskCompletionSource<IEnumerable<PowerTrade>>();
        var mockPowerService = new Mock<IPowerService>();
        mockPowerService
              .Setup(s => s.GetTradesAsync(now.Date))
              .ReturnsAsync(nativePowerTrades);


        var client = new PowerServiceClient(mockPowerService.Object);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await client.Invoking(c => c.GetTradesAsync(now, cts.Token))
                    .Should()
                    .ThrowAsync<OperationCanceledException>();
    }

    private static PowerTrade[] GetNateivePoverTrades(DateTime now) {
        var nativePowerTrade = PowerTrade.Create(now.Date, 2);
        nativePowerTrade.Periods[0].Volume = 10;
        nativePowerTrade.Periods[1].Volume = 20;
        var nativePowerTrades = new[] { nativePowerTrade };
        return nativePowerTrades;
    }


}
