namespace PowerTrading.Reporting.IntraDayReport;

public interface ITime {
    public DateTime GetTime(DateTime? utcTime=null);
}
