namespace PowerTrading.Domain; 
public class PowerPosition {
    public int Period { get; set; }
    public int Hour => Period == 1 ? 23 : Period - 2;   // period 1 => 23:00, period 2 => 00:00 ... period 24 => 22:00 same day
    public double Volume { get; set; }
}

 