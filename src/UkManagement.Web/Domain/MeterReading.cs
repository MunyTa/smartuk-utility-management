namespace UkManagement.Web.Domain;

public sealed class MeterReading
{
    public long Id { get; set; }

    public int MeterId { get; set; }
    public Meter Meter { get; set; } = null!;

    public decimal Value { get; set; }

    public DateTimeOffset MeasuredAt { get; set; }

    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;

    public ReadingQuality Quality { get; set; } = ReadingQuality.Normal;

    public int? SignalRssi { get; set; }

    public decimal? BatteryVoltage { get; set; }
}
