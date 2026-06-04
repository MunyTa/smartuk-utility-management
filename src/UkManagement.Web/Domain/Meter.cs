using System.ComponentModel.DataAnnotations;

namespace UkManagement.Web.Domain;

public sealed class Meter
{
    public int Id { get; set; }

    [MaxLength(80)]
    public required string SerialNumber { get; set; }

    [MaxLength(120)]
    public required string ExternalDeviceId { get; set; }

    public MeterType Type { get; set; }

    [MaxLength(24)]
    public required string Unit { get; set; }

    public MeterStatus Status { get; set; } = MeterStatus.Offline;

    public decimal? LastValue { get; set; }

    public DateTimeOffset? LastReadingAt { get; set; }

    public int ApartmentId { get; set; }
    public Apartment Apartment { get; set; } = null!;

    public ICollection<MeterReading> Readings { get; set; } = [];
}
