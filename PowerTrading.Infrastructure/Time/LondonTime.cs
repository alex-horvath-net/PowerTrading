using PowerTrading.Reporting.IntraDayReport;

namespace PowerTrading.Infrastructure.Time;
public class LondonTime : ITime {
    public DateTime GetTime(DateTime? time = null) {
        if (time == null)
            time = DateTime.UtcNow;

        // Determine date for trades: today’s date local London time
        var londonTimeZone = TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");
        var londonTime = TimeZoneInfo.ConvertTime(time.Value, londonTimeZone);
        return londonTime;
    }
}
