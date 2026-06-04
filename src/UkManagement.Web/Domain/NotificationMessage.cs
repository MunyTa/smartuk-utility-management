using System.ComponentModel.DataAnnotations;

namespace UkManagement.Web.Domain;

public sealed class NotificationMessage
{
    public long Id { get; set; }

    public int ResidentId { get; set; }
    public Resident Resident { get; set; } = null!;

    public NotificationChannel Channel { get; set; }

    [MaxLength(180)]
    public required string Subject { get; set; }

    [MaxLength(2000)]
    public required string Body { get; set; }

    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;

    [MaxLength(1200)]
    public string? DeliveryDetails { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? SentAt { get; set; }

    public DateTimeOffset? ReadAt { get; set; }
}
