using System.ComponentModel.DataAnnotations;

namespace UkManagement.Web.Domain;

public sealed class AuditLogEntry
{
    public long Id { get; set; }

    [MaxLength(80)]
    public required string ActorUserName { get; set; }

    [MaxLength(40)]
    public required string ActorRole { get; set; }

    [MaxLength(80)]
    public required string ActionType { get; set; }

    [MaxLength(180)]
    public required string EntityName { get; set; }

    [MaxLength(80)]
    public string? EntityId { get; set; }

    [MaxLength(1000)]
    public required string Details { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
