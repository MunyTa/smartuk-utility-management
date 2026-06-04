using System.Security.Claims;
using UkManagement.Web.Data;
using UkManagement.Web.Domain;

namespace UkManagement.Web.Services;

public sealed class AuditLogService(AppDbContext db)
{
    public async Task LogAsync(
        ClaimsPrincipal user,
        string actionType,
        string entityName,
        string details,
        string? entityId = null,
        CancellationToken cancellationToken = default)
    {
        db.AuditLogEntries.Add(new AuditLogEntry
        {
            ActorUserName = GetUserName(user),
            ActorRole = GetRole(user),
            ActionType = actionType,
            EntityName = entityName,
            EntityId = entityId,
            Details = details,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task LogSystemAsync(
        string actionType,
        string entityName,
        string details,
        string? entityId = null,
        CancellationToken cancellationToken = default)
    {
        db.AuditLogEntries.Add(new AuditLogEntry
        {
            ActorUserName = "system",
            ActorRole = "System",
            ActionType = actionType,
            EntityName = entityName,
            EntityId = entityId,
            Details = details,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    private static string GetUserName(ClaimsPrincipal user)
    {
        return user.Identity?.Name
            ?? user.FindFirstValue("preferred_username")
            ?? "unknown";
    }

    private static string GetRole(ClaimsPrincipal user)
    {
        if (user.IsInRole("Admin"))
        {
            return "Admin";
        }

        if (user.IsInRole("Dispatcher"))
        {
            return "Dispatcher";
        }

        return user.IsInRole("Resident") ? "Resident" : "User";
    }
}
