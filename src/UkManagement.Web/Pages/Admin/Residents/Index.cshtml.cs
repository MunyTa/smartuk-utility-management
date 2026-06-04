using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UkManagement.Web.Data;
using ResidentEntity = UkManagement.Web.Domain.Resident;

namespace UkManagement.Web.Pages.Admin.Residents;

public sealed class IndexModel(AppDbContext db) : PageModel
{
    public IReadOnlyList<ResidentEntity> Residents { get; private set; } = [];

    [TempData]
    public string? CreatedAccountMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Residents = await db.Residents
            .Include(x => x.Apartment)
            .ThenInclude(x => x.Building)
            .OrderBy(x => x.Apartment.Building.Address)
            .ThenBy(x => x.Apartment.Number)
            .ThenBy(x => x.FullName)
            .ToListAsync(cancellationToken);
    }
}
