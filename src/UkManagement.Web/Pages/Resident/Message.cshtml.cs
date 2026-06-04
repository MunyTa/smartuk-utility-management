using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using UkManagement.Web.Data;
using UkManagement.Web.Domain;
using UkManagement.Web.Services;

namespace UkManagement.Web.Pages.ResidentArea;

public sealed class MessageModel(
    AppDbContext db,
    CurrentResidentService currentResident) : PageModel
{
    public NotificationMessage Message { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(long id, CancellationToken cancellationToken)
    {
        var resident = await currentResident.GetRequiredAsync(User, cancellationToken);

        var message = await db.Notifications
            .FirstOrDefaultAsync(
                x => x.Id == id && x.ResidentId == resident.Id,
                cancellationToken);

        if (message is null)
        {
            return NotFound();
        }

        if (message.ReadAt is null)
        {
            message.ReadAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        Message = message;
        return Page();
    }
}
