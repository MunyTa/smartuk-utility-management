using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using UkManagement.Web.Data;
using UkManagement.Web.Domain;
using UkManagement.Web.Services;
using ResidentEntity = UkManagement.Web.Domain.Resident;

namespace UkManagement.Web.Pages.ResidentArea;

public sealed class IndexModel(
    AppDbContext db,
    CurrentResidentService currentResident) : PageModel
{
    public ResidentEntity ResidentProfile { get; private set; } = null!;
    public IReadOnlyList<Meter> Meters { get; private set; } = [];
    public IReadOnlyList<MeterReading> LatestReadings { get; private set; } = [];
    public IReadOnlyList<ServiceRequest> LatestRequests { get; private set; } = [];
    public IReadOnlyList<NotificationMessage> LatestMessages { get; private set; } = [];
    public int UnreadMessageCount { get; private set; }
    public string EmergencyNotificationSummary { get; private set; } = string.Empty;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        ResidentProfile = await currentResident.GetRequiredAsync(User, cancellationToken);
        EmergencyNotificationSummary = BuildEmergencyNotificationSummary(ResidentProfile);

        Meters = await db.Meters
            .Where(x => x.ApartmentId == ResidentProfile.ApartmentId)
            .OrderBy(x => x.Type)
            .ToListAsync(cancellationToken);

        var meterIds = Meters.Select(x => x.Id).ToArray();
        LatestReadings = await db.MeterReadings
            .Include(x => x.Meter)
            .Where(x => meterIds.Contains(x.MeterId))
            .OrderByDescending(x => x.MeasuredAt)
            .Take(8)
            .ToListAsync(cancellationToken);

        LatestRequests = await db.ServiceRequests
            .Where(x => x.ResidentId == ResidentProfile.Id)
            .OrderByDescending(x => x.CreatedAt)
            .Take(5)
            .ToListAsync(cancellationToken);

        var messagesQuery = db.Notifications
            .Where(x => x.ResidentId == ResidentProfile.Id);

        UnreadMessageCount = await messagesQuery
            .CountAsync(x => x.ReadAt == null, cancellationToken);

        LatestMessages = await messagesQuery
            .OrderByDescending(x => x.CreatedAt)
            .Take(5)
            .ToListAsync(cancellationToken);
    }

    private static string BuildEmergencyNotificationSummary(ResidentEntity resident)
    {
        if (!resident.EmergencyNotificationsEnabled)
        {
            return "отключены";
        }

        var channels = new List<string>();
        if (resident.EmergencyEmailEnabled)
        {
            channels.Add("Email");
        }

        if (resident.EmergencySmsEnabled)
        {
            channels.Add("SMS");
        }

        if (resident.EmergencyPushEnabled)
        {
            channels.Add("сообщение в профиле и Push");
        }

        return channels.Count == 0 ? "каналы не выбраны" : string.Join(", ", channels);
    }
}
