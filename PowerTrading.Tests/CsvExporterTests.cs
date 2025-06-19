using Microsoft.Extensions.Options;
using PowerTrading.Infrastructure.Csv;
using PowerTrading.Reporting.IntraDayReport;

namespace PowerTrading.Tests;
public class CsvExporterTests {
    [Fact]
    public async Task Export_CreatesCsvFileWithCorrectContent() {
        // Arrange
        var now = new DateTime(2025, 6, 17, 21, 34, 0);
        var mockTime = new Mock<ITime>();
        mockTime
            .Setup(t => t.GetTime(It.IsAny<DateTime?>()))
            .Returns(now);

        var options = Options.Create(new CsvExporterSettings {
            OutputFolder = Directory.GetCurrentDirectory(),
            Separator = ";",
            DecimalPlaces = 3
        });
        var exporter = new CsvExporter(mockTime.Object, options);
        var powerPositions = new List<Domain.PowerPosition>
        {
            new Domain.PowerPosition { Period = 1, Volume = 100 },
            new Domain.PowerPosition { Period = 2, Volume = 150 }
        };

        // Act
        var reportPath = await exporter.Export(powerPositions, CancellationToken.None);

        // Assert
        reportPath.Should().EndWith("PowerPosition_20250617_2134.csv");
        var reportLines = File.ReadAllLines(reportPath);
        reportLines[0].Should().Be("Local Time;Volume");
        reportLines[1].Should().Be("23:00;100.000");
        reportLines[2].Should().Be("00:00;150.000");

        if (File.Exists(reportPath))
            File.Delete(reportPath);
    }
}
