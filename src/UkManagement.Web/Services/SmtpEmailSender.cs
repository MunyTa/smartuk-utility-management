using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace UkManagement.Web.Services;

public sealed class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    private readonly SmtpOptions _options = options.Value;

    public async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromEmail));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        var socketOptions = _options.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.Auto;
        await client.ConnectAsync(_options.Host, _options.Port, socketOptions, cancellationToken);

        if (!string.IsNullOrWhiteSpace(_options.UserName) && !string.IsNullOrWhiteSpace(_options.Password))
        {
            await client.AuthenticateAsync(_options.UserName, _options.Password, cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
