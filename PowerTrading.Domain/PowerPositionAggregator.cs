using System.Collections.Concurrent;

namespace PowerTrading.Domain;
public class PowerPositionAggregator {

    public List<PowerPosition> AggregateByHour(IEnumerable<PowerTrade> trades, Guid runId, DateTime runTime) {
        if (trades == null)
            throw new ArgumentNullException(nameof(trades));

        var hourlyVolumes = trades
            .SelectMany(powerTrade => powerTrade.Periods)
            .GroupBy(powerPeriod => powerPeriod.Period)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(powerPeriod => powerPeriod.Volume));

        var powerPositions = Enumerable.Range(1, 24)
            .Select(period => new PowerPosition {
                Period = period,
                Volume = hourlyVolumes.TryGetValue(period, out var volume) ? volume : 0
            })
            .ToList();  

        return powerPositions;
    }

    public List<PowerPosition> AggregateByHour_Experiment(IEnumerable<PowerTrade> trades) {
        if (trades == null)
            throw new ArgumentNullException(nameof(trades));

        // Use stackalloc for fixed-size array on the stack (if method size allows)
        Span<double> hourlyVolumes = stackalloc double[25]; // Index 0 unused

        // Use a foreach loop with local variables to avoid repeated property accesses
        foreach (var trade in trades) {
            var periods = trade.Periods;
            for (int i = 0; i < periods.Length; i++) {
                var period = periods[i];
                int p = period.Period;
                if ((uint)(p - 1) < 24) // faster bounds check
                {
                    hourlyVolumes[p] += period.Volume;
                }
            }
        }

        var powerPositions = new List<PowerPosition>(24);
        for (int i = 1; i <= 24; i++) {
            powerPositions.Add(new PowerPosition {
                Period = i,
                Volume = hourlyVolumes[i]
            });
        }

        return powerPositions;
    }

}
