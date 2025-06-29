﻿using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PowerTrading.Infrastructure.Csv;
using PowerTrading.Reporting.IntraDayReport;

namespace PowerTrading.Tests;
public class CsvExporterTests {
    private readonly Mock<ILogger<CsvExporter>> _mockLogger = new Mock<ILogger<CsvExporter>>();

    [Fact]
    public async Task Export_CreatesCsvFileWithCorrectContent() {
        // Arrange
        var runId = Guid.Empty;
        var runTime = new DateTime(2025, 6, 17, 21, 34, 0);


        var options = Options.Create(new CsvExporterSettings {
            OutputFolder = Directory.GetCurrentDirectory(),
            Separator = ";",
            DecimalPlaces = 3
        });
        var exporter = new CsvExporter(options, _mockLogger.Object);
        var powerPositions = new List<Domain.PowerPosition>
        {
            new Domain.PowerPosition { Period = 1, Volume = 100 },
            new Domain.PowerPosition { Period = 2, Volume = 150 }
        };

        // Act
        var reportPath = await exporter.Export(powerPositions,runId, runTime, CancellationToken.None);

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
