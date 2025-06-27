using PowerTrading.Domain;

namespace PowerTrading.Reporting.IntraDayReport {
    public interface ICsvExporter {
        Task<string> Export(List<PowerPosition> powerPositions, Guid runId, DateTime runTime, CancellationToken token);
    }
}