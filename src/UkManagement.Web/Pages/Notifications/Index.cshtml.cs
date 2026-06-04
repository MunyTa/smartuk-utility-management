using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using UkManagement.Web.Data;
using UkManagement.Web.Domain;

namespace UkManagement.Web.Pages.Notifications;

public sealed class IndexModel(AppDbContext db) : PageModel
{
    public IReadOnlyList<NotificationMessage> Notifications { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Notifications = await db.Notifications
            .Include(x => x.Resident)
            .ThenInclude(x => x.Apartment)
            .OrderByDescending(x => x.CreatedAt)
            .Take(100)
            .ToListAsync();
    }
}
