using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using UkManagement.Web.Data;
using UkManagement.Web.Domain;

namespace UkManagement.Web.Pages;

public sealed class IndexModel(AppDbContext db) : PageModel
{
    public int BuildingCount { get; private set; }
    public int ApartmentCount { get; private set; }
    public int ResidentCount { get; private set; }
    public int MeterCount { get; private set; }
    public int WarningMeterCount { get; private set; }
    public int ActiveRequestCount { get; private set; }
    public int TotalRequestCount { get; private set; }
    public int PendingNotificationCount { get; private set; }
    public int ReadingsTodayCount { get; private set; }
    public int AnomalyReadingsTodayCount { get; private set; }
    public int NewRequestCount { get; private set; }
    public int InProgressRequestCount { get; private set; }
    public int CompletedRequestsTodayCount { get; private set; }
    public int SentNotificationsTodayCount { get; private set; }
    public IReadOnlyList<MeterReading> LatestReadings { get; private set; } = [];
    public IReadOnlyList<ServiceRequest> LatestServiceRequests { get; private set; } = [];
    public IReadOnlyList<NotificationMessage> LatestNotifications { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Page();
        }

        if (User.IsInRole("Resident")
            && !User.IsInRole("Admin")
            && !User.IsInRole("Dispatcher"))
        {
            return RedirectToPage("/Resident/Index");
        }

        var isAdmin = User.IsInRole("Admin");
        var isDispatcher = User.IsInRole("Dispatcher") && !isAdmin;

        var today = DisplayTime.Today;
        var todayStart = DisplayTime.StartOfDayUtc(today);
        var tomorrowStart = todayStart.AddDays(1);

        if (isAdmin)
        {
            BuildingCount = await db.Buildings.CountAsync();
            ApartmentCount = await db.Apartments.CountAsync();
            ResidentCount = await db.Residents.CountAsync();
            MeterCount = await db.Meters.CountAsync();
            PendingNotificationCount = await db.Notifications.CountAsync(x => x.Status == NotificationStatus.Pending);
            SentNotificationsTodayCount = await db.Notifications.CountAsync(x =>
                x.SentAt >= todayStart && x.SentAt < tomorrowStart && x.Status == NotificationStatus.Sent);

            LatestNotifications = await db.Notifications
                .Include(x => x.Resident)
                .ThenInclude(x => x.Apartment)
                .OrderByDescending(x => x.CreatedAt)
                .Take(6)
                .ToListAsync();
        }

        if (isDispatcher)
        {
            NewRequestCount = await db.ServiceRequests.CountAsync(x =>
                x.Status == ServiceRequestStatus.New
                && x.Category != ServiceRequestCategory.MeterInstallation
                && x.Category != ServiceRequestCategory.MeterReplacement);
            TotalRequestCount = await db.ServiceRequests.CountAsync(x =>
                x.Category != ServiceRequestCategory.MeterInstallation
                && x.Category != ServiceRequestCategory.MeterReplacement);

            LatestServiceRequests = await db.ServiceRequests
                .Include(x => x.Resident)
                .ThenInclude(x => x.Apartment)
                .Where(x => x.Category != ServiceRequestCategory.MeterInstallation
                    && x.Category != ServiceRequestCategory.MeterReplacement)
                .OrderByDescending(x => x.CreatedAt)
                .Take(5)
                .ToListAsync();
        }

        return Page();
    }
}
