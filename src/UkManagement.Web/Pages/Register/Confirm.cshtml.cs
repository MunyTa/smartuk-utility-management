using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using UkManagement.Web.Data;
using UkManagement.Web.Domain;

namespace UkManagement.Web.Pages.Register;

public sealed class ConfirmModel(AppDbContext db) : PageModel
{
    public ResidentRegistrationRequest RegistrationRequest { get; private set; } = null!;

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    [BindProperty]
    public ConfirmForm Form { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var request = await LoadRequestAsync(cancellationToken);
        if (request is null)
        {
            return NotFound();
        }

        RegistrationRequest = request;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var request = await LoadRequestAsync(cancellationToken);
        if (request is null)
        {
            return NotFound();
        }

        RegistrationRequest = request;
        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (request.Status != RegistrationRequestStatus.EmailCodeSent)
        {
            return RedirectToPage("/Register/Status", new { id = request.Id });
        }

        if (request.VerificationCodeExpiresAt < DateTimeOffset.UtcNow)
        {
            ModelState.AddModelError(nameof(Form.Code), "Код истек. Зарегистрируйтесь повторно, чтобы получить новый код.");
            return Page();
        }

        if (!request.IsCodeValid(Form.Code))
        {
            ModelState.AddModelError(nameof(Form.Code), "Неверный код подтверждения.");
            return Page();
        }

        request.Status = RegistrationRequestStatus.PendingApproval;
        request.EmailVerifiedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return RedirectToPage("/Register/Status", new { id = request.Id });
    }

    private async Task<ResidentRegistrationRequest?> LoadRequestAsync(CancellationToken cancellationToken)
    {
        return await db.RegistrationRequests.FirstOrDefaultAsync(x => x.Id == Id, cancellationToken);
    }

    public sealed class ConfirmForm
    {
        [Required(ErrorMessage = "Введите код.")]
        [RegularExpression("^[0-9]{5}$", ErrorMessage = "Код состоит из 5 цифр.")]
        public string Code { get; set; } = string.Empty;
    }
}
