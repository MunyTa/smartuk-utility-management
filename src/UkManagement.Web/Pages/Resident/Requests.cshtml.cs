using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using UkManagement.Web.Data;
using UkManagement.Web.Domain;
using UkManagement.Web.Services;
using ResidentEntity = UkManagement.Web.Domain.Resident;

namespace UkManagement.Web.Pages.ResidentArea;

public sealed class RequestsModel(
    AppDbContext db,
    CurrentResidentService currentResident) : PageModel
{
    public ResidentEntity ResidentProfile { get; private set; } = null!;
    public IReadOnlyList<ServiceRequest> ServiceRequests { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        ResidentProfile = await currentResident.GetRequiredAsync(User, cancellationToken);
        ServiceRequests = await db.ServiceRequests
            .Where(x => x.ResidentId == ResidentProfile.Id)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
