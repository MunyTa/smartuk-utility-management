using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using UkManagement.Web.Data;
using UkManagement.Web.Domain;

namespace UkManagement.Web.Pages.Register;

public sealed class StatusModel(AppDbContext db) : PageModel
{
    public ResidentRegistrationRequest RegistrationRequest { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        var request = await db.RegistrationRequests.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (request is null)
        {
            return NotFound();
        }

        RegistrationRequest = request;
        return Page();
    }
}
