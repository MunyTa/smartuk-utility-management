using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using UkManagement.Web.Data;
using UkManagement.Web.Domain;
using UkManagement.Web.Services;
using ResidentEntity = UkManagement.Web.Domain.Resident;

namespace UkManagement.Web.Pages.ResidentArea;

public sealed class MessagesModel(
    AppDbContext db,
    CurrentResidentService currentResident) : PageModel
{
    public ResidentEntity ResidentProfile { get; private set; } = null!;
    public IReadOnlyList<NotificationMessage> Messages { get; private set; } = [];
    public int TotalMessageCount { get; private set; }
    public int UnreadCount { get; private set; }
    public int PushSubscriptionCount { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        ResidentProfile = await currentResident.GetRequiredAsync(User, cancellationToken);

        var messagesQuery = db.Notifications
            .Where(x => x.ResidentId == ResidentProfile.Id);

        TotalMessageCount = await messagesQuery.CountAsync(cancellationToken);

        UnreadCount = await messagesQuery
            .CountAsync(x => x.ReadAt == null, cancellationToken);

        PushSubscriptionCount = await db.PushSubscriptions
            .CountAsync(x => x.ResidentId == ResidentProfile.Id, cancellationToken);

        Messages = await messagesQuery
            .OrderByDescending(x => x.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostMarkAllReadAsync(CancellationToken cancellationToken)
    {
        var resident = await currentResident.GetRequiredAsync(User, cancellationToken);
        var unreadMessages = await db.Notifications
            .Where(x => x.ResidentId == resident.Id && x.ReadAt == null)
            .ToListAsync(cancellationToken);

        if (unreadMessages.Count > 0)
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var message in unreadMessages)
            {
                message.ReadAt = now;
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        TempData["StatusMessage"] = "Все сообщения отмечены как прочитанные.";
        return RedirectToPage();
    }
}
