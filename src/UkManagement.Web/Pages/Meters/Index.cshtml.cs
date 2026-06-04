using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using UkManagement.Web.Data;
using UkManagement.Web.Domain;

namespace UkManagement.Web.Pages.Meters;

public sealed class IndexModel(AppDbContext db) : PageModel
{
    public IReadOnlyList<Meter> Meters { get; private set; } = [];
    public IReadOnlyList<SelectListItem> ApartmentOptions { get; private set; } = [];

    [BindProperty(SupportsGet = true)]
    public int? ApartmentId { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        ApartmentOptions = await db.Apartments
            .Include(x => x.Building)
            .OrderBy(x => x.Building.Address)
            .ThenBy(x => x.Number)
            .Select(x => new SelectListItem($"{x.Building.Address}, кв. {x.Number}", x.Id.ToString()))
            .ToListAsync(cancellationToken);

        var query = db.Meters
            .Include(x => x.Apartment)
            .ThenInclude(x => x.Building)
            .AsQueryable();

        if (ApartmentId is not null)
        {
            query = query.Where(x => x.ApartmentId == ApartmentId.Value);
        }

        Meters = await query
            .OrderBy(x => x.Apartment.Building.Address)
            .ThenBy(x => x.Apartment.Number)
            .ThenBy(x => x.Type)
            .ToListAsync(cancellationToken);
    }
}
