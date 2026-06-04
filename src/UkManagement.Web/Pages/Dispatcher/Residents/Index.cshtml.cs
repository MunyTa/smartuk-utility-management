using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using UkManagement.Web.Data;
using ResidentEntity = UkManagement.Web.Domain.Resident;

namespace UkManagement.Web.Pages.Dispatcher.Residents;

public sealed class IndexModel(AppDbContext db) : PageModel
{
    public IReadOnlyList<ResidentEntity> Residents { get; private set; } = [];

    [BindProperty(SupportsGet = true)]
    public string Sort { get; set; } = "apartment";

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var residents = await db.Residents
            .Include(x => x.Apartment)
            .ThenInclude(x => x.Building)
            .ToListAsync(cancellationToken);

        Residents = Sort == "name"
            ? residents
                .OrderBy(x => x.FullName)
                .ThenBy(x => ApartmentSortKey(x.Apartment.Number))
                .ToList()
            : residents
                .OrderBy(x => ApartmentSortKey(x.Apartment.Number))
                .ThenBy(x => x.FullName)
                .ToList();
    }

    private static int ApartmentSortKey(string apartmentNumber)
    {
        var digits = new string(apartmentNumber.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var number) ? number : int.MaxValue;
    }
}
