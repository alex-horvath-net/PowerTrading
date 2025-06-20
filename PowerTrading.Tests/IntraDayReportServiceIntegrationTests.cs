using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PowerTrading.Domain;
using PowerTrading.Infrastructure.Csv;
using PowerTrading.Reporting.IntraDayReport;

namespace PowerTrading.Tests;
public class IntraDayReportServiceIntegrationTests {
    private readonly Mock<ILogger<CsvExporter>> _mockLogger = new Mock<ILogger<CsvExporter>>();

    [Fact]
    public async Task GenerateAsync_CreatesCsvFile_WithCorrectContent() {
        // Arrange
        var tempFolder = Path.Combine(Path.GetTempPath(), "PowerPositionReports");
        Directory.CreateDirectory(tempFolder);

        var csvSettings = Options.Create(new CsvExporterSettings {
            OutputFolder = tempFolder,
            Separator = ",",
            DecimalPlaces = 0
        });

        var now = new DateTime(2025, 6, 17, 21, 34, 0);
        var mockTime = new Mock<ITime>();
        mockTime
            .Setup(t => t.GetTime(It.IsAny<DateTime?>()))
            .Returns(now);
        var csvExporter = new CsvExporter(mockTime.Object, csvSettings, _mockLogger.Object);

        var dummyTrades = new List<PowerTrade>
        {
            new PowerTrade
            {
                Date =DateTime.Parse("01/04/2015"),
                Periods = new[]
                {
                    new PowerPeriod { Period = 1, Volume = 100 },
                    new PowerPeriod { Period = 2, Volume = 100 },
                    new PowerPeriod { Period = 3, Volume = 100 },
                    new PowerPeriod { Period = 4, Volume = 100 },
                    new PowerPeriod { Period = 5, Volume = 100 },
                    new PowerPeriod { Period = 6, Volume = 100 },
                    new PowerPeriod { Period = 7, Volume = 100 },
                    new PowerPeriod { Period = 8, Volume = 100 },
                    new PowerPeriod { Period = 9, Volume = 100 },
                    new PowerPeriod { Period = 10, Volume = 100 },
                    new PowerPeriod { Period = 11, Volume = 100 },
                    new PowerPeriod { Period = 12, Volume = 100 },
                    new PowerPeriod { Period = 13, Volume = 100 },
                    new PowerPeriod { Period = 14, Volume = 100 },
                    new PowerPeriod { Period = 15, Volume = 100 },
                    new PowerPeriod { Period = 16, Volume = 100 },
                    new PowerPeriod { Period = 17, Volume = 100 },
                    new PowerPeriod { Period = 18, Volume = 100 },
                    new PowerPeriod { Period = 19, Volume = 100 },
                    new PowerPeriod { Period = 20, Volume = 100 },
                    new PowerPeriod { Period = 21, Volume = 100 },
                    new PowerPeriod { Period = 22, Volume = 100 },
                    new PowerPeriod { Period = 23, Volume = 100 },
                    new PowerPeriod { Period = 24, Volume = 100 }
                }
            },
            new PowerTrade
            {
                Date =DateTime.Parse("01/04/2015"),
                Periods = new[]
                {
                    new PowerPeriod { Period = 1, Volume = 50 },
                    new PowerPeriod { Period = 2, Volume = 50 },
                    new PowerPeriod { Period = 3, Volume = 50 },
                    new PowerPeriod { Period = 4, Volume = 50 },
                    new PowerPeriod { Period = 5, Volume = 50 },
                    new PowerPeriod { Period = 6, Volume = 50 },
                    new PowerPeriod { Period = 7, Volume = 50 },
                    new PowerPeriod { Period = 8, Volume = 50 },
                    new PowerPeriod { Period = 9, Volume = 50 },
                    new PowerPeriod { Period = 10, Volume = 50 },
                    new PowerPeriod { Period = 11, Volume = 50 },
                    new PowerPeriod { Period = 12, Volume = -20 },
                    new PowerPeriod { Period = 13, Volume = -20 },
                    new PowerPeriod { Period = 14, Volume = -20 },
                    new PowerPeriod { Period = 15, Volume = -20 },
                    new PowerPeriod { Period = 16, Volume = -20 },
                    new PowerPeriod { Period = 17, Volume = -20 },
                    new PowerPeriod { Period = 18, Volume = -20 },
                    new PowerPeriod { Period = 19, Volume = -20 },
                    new PowerPeriod { Period = 20, Volume = -20 },
                    new PowerPeriod { Period = 21, Volume = -20 },
                    new PowerPeriod { Period = 22, Volume = -20 },
                    new PowerPeriod { Period = 23, Volume = -20 },
                    new PowerPeriod { Period = 24, Volume = -20 }
                }
            }
        };



        var mockPowerServiceClient = new Mock<IPowerServiceClient>();
        mockPowerServiceClient.Setup(c => c.GetTradesAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                              .ReturnsAsync(dummyTrades);

        var service = new IntraDayReportService(mockPowerServiceClient.Object, csvExporter);

        // Act
        var reportPath = await service.GenerateAsync(now, CancellationToken.None);
        var reportLines = await File.ReadAllLinesAsync(reportPath);

        // Assert
        File.Exists(reportPath).Should().BeTrue();


        reportLines[0].Should().Be("Local Time,Volume");
        reportLines[1].Should().Be("23:00,150");
        reportLines[2].Should().Be("00:00,150");
        reportLines[3].Should().Be("01:00,150");
        reportLines[4].Should().Be("02:00,150");
        reportLines[5].Should().Be("03:00,150");
        reportLines[6].Should().Be("04:00,150");
        reportLines[7].Should().Be("05:00,150");
        reportLines[8].Should().Be("06:00,150");
        reportLines[9].Should().Be("07:00,150");
        reportLines[10].Should().Be("08:00,150");
        reportLines[11].Should().Be("09:00,150");
        reportLines[12].Should().Be("10:00,80");
        reportLines[13].Should().Be("11:00,80");
        reportLines[14].Should().Be("12:00,80");
        reportLines[15].Should().Be("13:00,80");
        reportLines[16].Should().Be("14:00,80");
        reportLines[17].Should().Be("15:00,80");
        reportLines[18].Should().Be("16:00,80");
        reportLines[19].Should().Be("17:00,80");
        reportLines[20].Should().Be("18:00,80");
        reportLines[21].Should().Be("19:00,80");
        reportLines[22].Should().Be("20:00,80");
        reportLines[23].Should().Be("21:00,80");
        reportLines[24].Should().Be("22:00,80");

        // Clean up
        File.Delete(reportPath);
        Directory.Delete(tempFolder, true);
    }
}
