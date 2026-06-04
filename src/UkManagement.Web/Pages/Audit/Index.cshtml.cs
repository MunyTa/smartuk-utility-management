using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using UkManagement.Web.Data;
using UkManagement.Web.Domain;

namespace UkManagement.Web.Pages.Audit;

public sealed class IndexModel(AppDbContext db) : PageModel
{
    public IReadOnlyList<AuditLogEntry> Entries { get; private set; } = [];

    [BindProperty(SupportsGet = true)]
    public string? Actor { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ActionType { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var query = db.AuditLogEntries.AsQueryable();

        if (!string.IsNullOrWhiteSpace(Actor))
        {
            query = query.Where(x => x.ActorUserName.Contains(Actor));
        }

        if (!string.IsNullOrWhiteSpace(ActionType))
        {
            query = query.Where(x => x.ActionType == ActionType);
        }

        Entries = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);
    }
}
