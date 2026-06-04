using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UkManagement.Web.Data;
using UkManagement.Web.Domain;
using WebPush;
using WebPushSubscription = WebPush.PushSubscription;

namespace UkManagement.Web.Services;

public sealed class PushNotificationService(
    AppDbContext db,
    IOptions<VapidOptions> options,
    ILogger<PushNotificationService> logger)
{
    public async Task<int> SendAsync(
        int residentId,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        var vapidOptions = options.Value;
        if (!vapidOptions.IsConfigured)
        {
            throw new InvalidOperationException("Не настроены VAPID-ключи для Web Push.");
        }

        var subscriptions = await db.PushSubscriptions
            .Where(x => x.ResidentId == residentId)
            .ToListAsync(cancellationToken);

        if (subscriptions.Count == 0)
        {
            return 0;
        }

        var vapidDetails = new VapidDetails(
            vapidOptions.Subject,
            vapidOptions.PublicKey,
            vapidOptions.PrivateKey);

        var payload = JsonSerializer.Serialize(new
        {
            title = subject,
            body,
            url = "/Resident/Messages"
        });

        var sentCount = 0;
        var errors = new List<string>();
        using var webPushClient = new WebPushClient();

        foreach (var savedSubscription in subscriptions)
        {
            var subscription = new WebPushSubscription(
                savedSubscription.Endpoint,
                savedSubscription.P256Dh,
                savedSubscription.Auth);

            try
            {
                await webPushClient.SendNotificationAsync(
                    subscription,
                    payload,
                    vapidDetails,
                    cancellationToken);
                sentCount++;
            }
            catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
            {
                logger.LogInformation(
                    ex,
                    "Removing expired push subscription {SubscriptionId}",
                    savedSubscription.Id);
                db.PushSubscriptions.Remove(savedSubscription);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Push delivery failed for resident {ResidentId}, subscription {SubscriptionId}",
                    residentId,
                    savedSubscription.Id);
                errors.Add(ex.Message);
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        if (sentCount == 0)
        {
            var errorText = errors.Count == 0
                ? "Все Web Push подписки устарели и были удалены."
                : string.Join("; ", errors.Distinct());
            throw new InvalidOperationException(errorText);
        }

        return sentCount;
    }
}
