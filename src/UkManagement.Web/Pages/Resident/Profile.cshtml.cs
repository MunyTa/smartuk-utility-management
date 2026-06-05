using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UkManagement.Web.Data;
using UkManagement.Web.Services;
using ResidentEntity = UkManagement.Web.Domain.Resident;

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
        Form.EmergencyNotificationsEnabled = resident.EmergencyNotificationsEnabled;
        Form.EmergencyEmailEnabled = resident.EmergencyEmailEnabled;
        Form.EmergencySmsEnabled = resident.EmergencySmsEnabled;
        Form.EmergencyPushEnabled = resident.EmergencyPushEnabled;
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var resident = await currentResident.GetRequiredAsync(User, cancellationToken);

        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (Form.EmergencyNotificationsEnabled
            && !Form.EmergencyEmailEnabled
            && !Form.EmergencySmsEnabled
            && !Form.EmergencyPushEnabled)
        {
            ModelState.AddModelError(
                string.Empty,
                "Выберите хотя бы один канал аварийных уведомлений или отключите их полностью.");
            return Page();
        }

        var oldEmail = resident.Email;
        var oldPhone = resident.Phone;
        var oldPreferences = FormatPreferences(resident);

        resident.Email = Form.Email.Trim();
        resident.Phone = Form.Phone.Trim();
        resident.EmergencyNotificationsEnabled = Form.EmergencyNotificationsEnabled;
        resident.EmergencyEmailEnabled = Form.EmergencyEmailEnabled;
        resident.EmergencySmsEnabled = Form.EmergencySmsEnabled;
        resident.EmergencyPushEnabled = Form.EmergencyPushEnabled;
        var newPreferences = FormatPreferences(resident);

        await db.SaveChangesAsync(cancellationToken);
        await auditLog.LogAsync(
            User,
            "ResidentProfileUpdated",
            "Resident",
            $"Житель обновил контакты: email {oldEmail} -> {resident.Email}, телефон {oldPhone} -> {resident.Phone}. Аварийные уведомления: {oldPreferences} -> {newPreferences}.",
            resident.Id.ToString(),
            cancellationToken);

        StatusMessage = "Контакты обновлены.";
        return RedirectToPage("/Resident/Index");
    }

    private static string FormatPreferences(ResidentEntity resident)
    {
        if (!resident.EmergencyNotificationsEnabled)
        {
            return "отключены";
        }

        var channels = new List<string>();
        if (resident.EmergencyEmailEnabled)
        {
            channels.Add("Email");
        }

        if (resident.EmergencySmsEnabled)
        {
            channels.Add("SMS");
        }

        if (resident.EmergencyPushEnabled)
        {
            channels.Add("Профиль/Push");
        }

        return channels.Count == 0 ? "каналы не выбраны" : string.Join(", ", channels);
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

        public bool EmergencyNotificationsEnabled { get; set; } = true;
        public bool EmergencyEmailEnabled { get; set; } = true;
        public bool EmergencySmsEnabled { get; set; }
        public bool EmergencyPushEnabled { get; set; }
    }
}
