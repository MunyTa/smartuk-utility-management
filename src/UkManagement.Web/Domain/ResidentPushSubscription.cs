using System.ComponentModel.DataAnnotations;

namespace UkManagement.Web.Domain;

public sealed class ResidentPushSubscription
{
    public long Id { get; set; }

    public int ResidentId { get; set; }
    public Resident Resident { get; set; } = null!;

    [MaxLength(2048)]
    public required string Endpoint { get; set; }

    [MaxLength(256)]
    public required string P256Dh { get; set; }

    [MaxLength(128)]
    public required string Auth { get; set; }

    [MaxLength(300)]
    public string? UserAgent { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;
}
