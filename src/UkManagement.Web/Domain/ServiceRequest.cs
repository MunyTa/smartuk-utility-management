using System.ComponentModel.DataAnnotations;

namespace UkManagement.Web.Domain;

public sealed class ServiceRequest
{
    public int Id { get; set; }

    public int ResidentId { get; set; }
    public Resident Resident { get; set; } = null!;

    public ServiceRequestCategory Category { get; set; } = ServiceRequestCategory.Other;
    public ServiceRequestPriority Priority { get; set; } = ServiceRequestPriority.Normal;
    public ServiceRequestStatus Status { get; set; } = ServiceRequestStatus.New;

    [MaxLength(180)]
    public required string Title { get; set; }

    [MaxLength(2000)]
    public required string Description { get; set; }

    [MaxLength(1000)]
    public string? DispatcherComment { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
}
