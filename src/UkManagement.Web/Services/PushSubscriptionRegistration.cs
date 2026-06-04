using System.ComponentModel.DataAnnotations;

namespace UkManagement.Web.Services;

public sealed class PushSubscriptionRegistration
{
    [Required, StringLength(2048)]
    public required string Endpoint { get; set; }

    [Required]
    public required PushSubscriptionKeys Keys { get; set; }

    [StringLength(300)]
    public string? UserAgent { get; set; }
}

public sealed class PushSubscriptionKeys
{
    [Required, StringLength(256)]
    public required string P256Dh { get; set; }

    [Required, StringLength(128)]
    public required string Auth { get; set; }
}
