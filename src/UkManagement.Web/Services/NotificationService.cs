using Microsoft.EntityFrameworkCore;
using UkManagement.Web.Data;
using UkManagement.Web.Domain;

namespace UkManagement.Web.Services;

public sealed class NotificationService(
    AppDbContext db,
    IEmailSender emailSender,
    ISmsSender smsSender,
    PushNotificationService pushNotificationService,
    ILogger<NotificationService> logger)
{
    public async Task<NotificationMessage> SendAsync(
        int residentId,
        NotificationChannel channel,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        var resident = await db.Residents.FirstAsync(x => x.Id == residentId, cancellationToken);
        var notification = new NotificationMessage
        {
            ResidentId = resident.Id,
            Channel = channel,
            Subject = subject,
            Body = body
        };

        db.Notifications.Add(notification);
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            var deliveryDetails = channel switch
            {
                NotificationChannel.Email => await SendEmailAsync(resident.Email, subject, body, cancellationToken),
                NotificationChannel.Push => await SendPushAsync(resident.Id, subject, body, cancellationToken),
                NotificationChannel.Sms => await SendSmsAsync(resident.Phone, subject, body, cancellationToken),
                _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, null)
            };

            notification.Status = NotificationStatus.Sent;
            notification.SentAt = DateTimeOffset.UtcNow;
            notification.DeliveryDetails = deliveryDetails;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Notification delivery failed for resident {ResidentId}", resident.Id);
            notification.Status = NotificationStatus.Failed;
            notification.DeliveryDetails = ex.Message;
        }

        await db.SaveChangesAsync(cancellationToken);
        return notification;

        async Task<string> SendEmailAsync(
            string to,
            string messageSubject,
            string messageBody,
            CancellationToken token)
        {
            await emailSender.SendAsync(to, messageSubject, messageBody, token);
            return "Доставлено через Email";
        }

        async Task<string> SendPushAsync(
            int targetResidentId,
            string messageSubject,
            string messageBody,
            CancellationToken token)
        {
            try
            {
                var sentCount = await pushNotificationService.SendAsync(
                    targetResidentId,
                    messageSubject,
                    messageBody,
                    token);

                return sentCount == 0
                    ? "Сохранено в профиле; браузерные уведомления не подключены"
                    : $"Сохранено в профиле; отправлено в браузер: {sentCount}";
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Browser push delivery failed for resident {ResidentId}, profile message is saved",
                    targetResidentId);
                return $"Сохранено в профиле; браузерное уведомление не отправлено: {ex.Message}";
            }
        }

        async Task<string> SendSmsAsync(
            string phone,
            string messageSubject,
            string messageBody,
            CancellationToken token)
        {
            var text = $"{messageSubject}. {messageBody}";
            if (text.Length > 500)
            {
                text = $"{text[..497]}...";
            }

            return await smsSender.SendAsync(phone, text, token);
        }
    }
}
