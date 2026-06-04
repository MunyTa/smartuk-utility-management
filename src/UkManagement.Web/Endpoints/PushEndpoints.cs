using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using UkManagement.Web.Data;
using UkManagement.Web.Domain;
using UkManagement.Web.Services;

namespace UkManagement.Web.Endpoints;

public static class PushEndpoints
{
    public static void MapPushEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/push/vapid-public-key", (
            IOptions<VapidOptions> options) =>
        {
            var vapidOptions = options.Value;
            return vapidOptions.IsConfigured
                ? Results.Ok(new { publicKey = vapidOptions.PublicKey })
                : Results.Problem("VAPID-ключи для Web Push не настроены.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }).RequireAuthorization("Resident");

        endpoints.MapPost("/api/push/subscriptions", async (
            [FromBody] PushSubscriptionRegistration registration,
            AppDbContext db,
            CurrentResidentService currentResident,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(registration.Endpoint)
                || registration.Keys is null
                || string.IsNullOrWhiteSpace(registration.Keys.P256Dh)
                || string.IsNullOrWhiteSpace(registration.Keys.Auth))
            {
                return Results.BadRequest(new { message = "Некорректные данные Web Push подписки." });
            }

            var resident = await currentResident.GetAsync(user, cancellationToken);
            if (resident is null)
            {
                return Results.Forbid();
            }

            var endpoint = registration.Endpoint.Trim();
            var subscription = await db.PushSubscriptions
                .FirstOrDefaultAsync(x => x.Endpoint == endpoint, cancellationToken);

            if (subscription is null)
            {
                subscription = new ResidentPushSubscription
                {
                    ResidentId = resident.Id,
                    Endpoint = endpoint,
                    P256Dh = registration.Keys.P256Dh,
                    Auth = registration.Keys.Auth,
                    UserAgent = registration.UserAgent,
                    CreatedAt = DateTimeOffset.UtcNow,
                    LastSeenAt = DateTimeOffset.UtcNow
                };
                db.PushSubscriptions.Add(subscription);
            }
            else
            {
                subscription.ResidentId = resident.Id;
                subscription.P256Dh = registration.Keys.P256Dh;
                subscription.Auth = registration.Keys.Auth;
                subscription.UserAgent = registration.UserAgent;
                subscription.LastSeenAt = DateTimeOffset.UtcNow;
            }

            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(new { subscription.Id });
        }).RequireAuthorization("Resident");
    }
}
