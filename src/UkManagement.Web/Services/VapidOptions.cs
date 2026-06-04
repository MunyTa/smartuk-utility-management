namespace UkManagement.Web.Services;

public sealed class VapidOptions
{
    public string? PublicKey { get; set; }
    public string? PrivateKey { get; set; }
    public string Subject { get; set; } = "mailto:dispatcher@uk.local";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(PublicKey)
        && !string.IsNullOrWhiteSpace(PrivateKey)
        && !string.IsNullOrWhiteSpace(Subject);
}
