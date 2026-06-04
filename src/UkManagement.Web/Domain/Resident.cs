using System.ComponentModel.DataAnnotations;

namespace UkManagement.Web.Domain;

public sealed class Resident
{
    public int Id { get; set; }

    [MaxLength(120)]
    public required string FullName { get; set; }

    [MaxLength(180)]
    public required string Email { get; set; }

    [MaxLength(32)]
    public required string Phone { get; set; }

    [MaxLength(80)]
    public string? KeycloakUsername { get; set; }

    public int ApartmentId { get; set; }
    public Apartment Apartment { get; set; } = null!;

    public ICollection<NotificationMessage> Notifications { get; set; } = [];
    public ICollection<ResidentPushSubscription> PushSubscriptions { get; set; } = [];
    public ICollection<ServiceRequest> ServiceRequests { get; set; } = [];
}
