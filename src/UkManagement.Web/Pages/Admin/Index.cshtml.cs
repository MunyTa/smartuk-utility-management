using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using UkManagement.Web.Data;
using UkManagement.Web.Domain;

namespace UkManagement.Web.Pages.Admin;

public sealed class IndexModel(AppDbContext db) : PageModel
{
    public int ResidentCount { get; private set; }
    public int ApartmentCount { get; private set; }
    public int MeterCount { get; private set; }
    public int PendingRegistrationCount { get; private set; }
    public int MeterRequestCount { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        ResidentCount = await db.Residents.CountAsync(cancellationToken);
        ApartmentCount = await db.Apartments.CountAsync(cancellationToken);
        MeterCount = await db.Meters.CountAsync(cancellationToken);
        PendingRegistrationCount = await db.RegistrationRequests.CountAsync(
            x => x.Status == RegistrationRequestStatus.PendingApproval,
            cancellationToken);
        MeterRequestCount = await db.ServiceRequests.CountAsync(
            x => (x.Category == ServiceRequestCategory.MeterInstallation
                    || x.Category == ServiceRequestCategory.MeterReplacement)
                 && x.Status != ServiceRequestStatus.Completed
                 && x.Status != ServiceRequestStatus.Cancelled,
            cancellationToken);
    }
}
