using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using UkManagement.Web.Data;
using UkManagement.Web.Domain;

namespace UkManagement.Web.Pages.Requests;

public sealed class IndexModel(AppDbContext db) : PageModel
{
    public IReadOnlyList<ServiceRequest> ServiceRequests { get; private set; } = [];
    public string PageTitle { get; private set; } = "Заявки жильцов";
    public string EmptyText { get; private set; } = "Заявки еще не создавались.";

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var isAdmin = User.IsInRole("Admin");
        var query = db.ServiceRequests
            .Include(x => x.Resident)
            .ThenInclude(x => x.Apartment)
            .ThenInclude(x => x.Building)
            .AsQueryable();

        if (isAdmin)
        {
            PageTitle = "Заявки на приборы учета";
            EmptyText = "Технических заявок на приборы пока нет.";
            query = query.Where(x =>
                x.Category == ServiceRequestCategory.MeterInstallation
                || x.Category == ServiceRequestCategory.MeterReplacement);
        }
        else
        {
            query = query.Where(x =>
                x.Category != ServiceRequestCategory.MeterInstallation
                && x.Category != ServiceRequestCategory.MeterReplacement);
        }

        ServiceRequests = await query
            .OrderBy(x => x.Status == ServiceRequestStatus.Completed || x.Status == ServiceRequestStatus.Cancelled)
            .ThenByDescending(x => x.Priority)
            .ThenByDescending(x => x.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);
    }
}
