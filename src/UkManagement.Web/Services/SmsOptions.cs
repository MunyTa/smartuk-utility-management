namespace UkManagement.Web.Services;

public sealed class SmsOptions
{
    public string? Login { get; set; }
    public string? ApiKey { get; set; }
    public string? Sender { get; set; }
    public bool TestMode { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Login) && !string.IsNullOrWhiteSpace(ApiKey);
}
