using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UkManagement.Web.Data;
using UkManagement.Web.Services;

namespace UkManagement.Web.Pages.ResidentArea;

public sealed class ProfileModel(
    AppDbContext db,
    CurrentResidentService currentResident,
    AuditLogService auditLog) : PageModel
{
    [TempData]
    public string? StatusMessage { get; set; }

    [BindProperty]
    public ProfileForm Form { get; set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var resident = await currentResident.GetRequiredAsync(User, cancellationToken);
        Form.Email = resident.Email;
        Form.Phone = resident.Phone;
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var resident = await currentResident.GetRequiredAsync(User, cancellationToken);

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var oldEmail = resident.Email;
        var oldPhone = resident.Phone;

        resident.Email = Form.Email.Trim();
        resident.Phone = Form.Phone.Trim();

        await db.SaveChangesAsync(cancellationToken);
        await auditLog.LogAsync(
            User,
            "ResidentProfileUpdated",
            "Resident",
            $"Житель обновил контакты: email {oldEmail} -> {resident.Email}, телефон {oldPhone} -> {resident.Phone}.",
            resident.Id.ToString(),
            cancellationToken);

        StatusMessage = "Контакты обновлены.";
        return RedirectToPage("/Resident/Index");
    }

    public sealed class ProfileForm
    {
        [Required(ErrorMessage = "Введите email.")]
        [EmailAddress(ErrorMessage = "Введите корректный email.")]
        [StringLength(180, ErrorMessage = "Email не должен быть длиннее {1} символов.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введите телефон.")]
        [StringLength(32, ErrorMessage = "Телефон не должен быть длиннее {1} символов.")]
        public string Phone { get; set; } = string.Empty;
    }
}
