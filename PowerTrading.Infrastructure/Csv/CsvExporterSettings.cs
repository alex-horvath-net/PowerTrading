namespace PowerTrading.Infrastructure.Csv;

public class CsvExporterSettings {
    public const string SectionName = "CsvExporter";
    public string OutputFolder { get; set; } = "C:\\";
    public string Separator { get; set; } = ";";
    public int DecimalPlaces { get; set; } = 3;
}
