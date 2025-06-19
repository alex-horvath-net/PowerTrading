using PowerTrading.Infrastructure.Clients;
using PowerTrading.Reporting.IntraDayReport;
using Services; // Namespace for native PowerService types

namespace PowerTrading.Tests;

public class PowerServiceClientTests {

    [Fact]
    public async Task GetTradesAsync_Normal_Is_Random() {
        // Arrange
        Environment.SetEnvironmentVariable("SERVICE_MODE", "Normal");  // / Normal , Error, Test
        var powerService = new PowerService();
        using var cts = new CancellationTokenSource();
        var now = new DateTime(2025, 6, 17, 10, 33, 0);
        var client = new PowerServiceClient(powerService);

        // Act
        var powerTrades = await client.GetTradesAsync(now, cts.Token);

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
        var now = new DateTime(2025, 6, 17, 10, 33, 0);
        var client = new PowerServiceClient(powerService);

        // Act
        var powerTrades = await client.GetTradesAsync(now, cts.Token);

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
        var now = new DateTime(2025, 6, 17, 10, 33, 0);
        var client = new PowerServiceClient(powerService);

        // Act
        await Assert.ThrowsAsync<PowerServiceException>( () =>  
            client.GetTradesAsync(now, cts.Token));

        // CleanUp
        Environment.SetEnvironmentVariable("SERVICE_MODE", "Normal");
    }


    [Fact]
    public async Task GetTradesAsync_ReturnsMappedTrades_WhenNotCancelled() {
        // Arrange
        var now = new DateTime(2025, 6, 17);

        Services.PowerTrade[] nativePowerTrades = GetNateivePoverTrades(now);

        var mockPowerService = new Mock<IPowerService>();
        mockPowerService
            .Setup(s => s.GetTradesAsync(now))
            .ReturnsAsync(nativePowerTrades);

        var client = new PowerServiceClient(mockPowerService.Object);
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

        Services.PowerTrade[] nativePowerTrades = GetNateivePoverTrades(now);


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

    private static Services.PowerTrade[] GetNateivePoverTrades(DateTime now) {
        var nativePowerTrade = Services.PowerTrade.Create(now.Date, 2);
        nativePowerTrade.Periods[0].Volume = 10;
        nativePowerTrade.Periods[1].Volume = 20;
        var nativePowerTrades = new[] { nativePowerTrade };
        return nativePowerTrades;
    }


}
