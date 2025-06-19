using System.Diagnostics;
using PowerTrading.Domain;

namespace PowerTrading.Tests;
public class PowerPositionAggregatorTests {
    [Fact]
    public void AggregateByHour_ShouldSumVolumesPerPeriod() {
        // Arrange
        var aggregator = new PowerPositionAggregator();

        var trades = new List<PowerTrade>
        {
            new PowerTrade
            {
                Periods = new[]
                {
                    new PowerPeriod { Period = 1, Volume = 100 },
                    new PowerPeriod { Period = 2, Volume = 150 }
                }
            },
            new PowerTrade
            {
                Periods = new[]
                {
                    new PowerPeriod { Period = 1, Volume = 50 },
                    new PowerPeriod { Period = 2, Volume = 25 }
                }
            }
        };

        // Act
        var powerPositions = aggregator.AggregateByHour(trades);

        // Assert
        powerPositions.Count.Should().Be(24); // Should have 24 periods
        powerPositions.Find(e => e.Period == 1)!.Volume.Should().Be(150);
        powerPositions.Find(e => e.Period == 2)!.Volume.Should().Be(175);
        powerPositions.Find(e => e.Period == 3)!.Volume.Should().Be(0);
    }

    [Fact]
    public void AggregateByHour_PerformanceTest() {
        // Arrange
        var trades = GenerateTrades(1_00_000);
        var aggregator = new PowerPositionAggregator();

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = aggregator.AggregateByHour(trades);
        stopwatch.Stop();

        // Assert basic correctness
        result.Should().HaveCount(24);
        result.Sum(p => p.Volume).Should().BeGreaterThan(0);

        // Assert performance 
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000);

        Console.WriteLine($"AggregateByHour processed 100k trades in {stopwatch.ElapsedMilliseconds} ms.");
    }

    private static List<PowerTrade> GenerateTrades(int count) {
        var rand = new Random(42);
        var trades = new List<PowerTrade>(count);

        for (int i = 0; i < count; i++) {
            var periods = new PowerPeriod[24];
            for (int p = 1; p <= 24; p++) {
                periods[p - 1] = new PowerPeriod {
                    Period = p,
                    Volume = rand.NextDouble() * 1000
                };
            }

            trades.Add(new PowerTrade {
                Date = DateTime.Today,
                Periods = periods
            });
        }

        return trades;
    }
}
