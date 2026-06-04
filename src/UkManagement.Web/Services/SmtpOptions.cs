namespace UkManagement.Web.Services;

public sealed class SmtpOptions
{
    public string Host { get; set; } = "smtp.mail.ru";
    public int Port { get; set; } = 2525;
    public bool UseSsl { get; set; }
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string FromEmail { get; set; } = "dispatcher@uk.local";
    public string FromName { get; set; } = "Диспетчер УК";
}
