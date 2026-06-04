using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using UkManagement.Web.Data;
using UkManagement.Web.Domain;
using UkManagement.Web.Services;

namespace UkManagement.Web.Pages.Readings;

public sealed class SettingsModel(
    AppDbContext db,
    MeterReadingRetentionService retentionService,
    AuditLogService auditLog) : PageModel
{
    public IReadOnlyList<SelectListItem> RetentionOptions { get; private set; } =
    [
        new("1 день", "1"),
        new("7 дней", "7"),
        new("30 дней", "30")
    ];

    public int CurrentRetentionDays { get; private set; }
    public int ReadingCount { get; private set; }
    public DateTimeOffset? OldestReadingReceivedAt { get; private set; }

    [TempData]
    public string? StatusMessage { get; set; }

    [BindProperty]
    public SettingsForm Form { get; set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadPageDataAsync(cancellationToken);
        Form.RetentionDays = CurrentRetentionDays;
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadPageDataAsync(cancellationToken);
            return Page();
        }

        var deleted = await retentionService.UpdateRetentionDaysAsync(Form.RetentionDays, cancellationToken);
        await auditLog.LogAsync(
            User,
            "ReadingsRetentionUpdated",
            "SystemSettings",
            $"Срок хранения показаний изменен на {Form.RetentionDays} дн., удалено старых записей: {deleted}.",
            "1",
            cancellationToken);
        StatusMessage = $"Срок хранения обновлен: {Form.RetentionDays} дн. Удалено старых записей: {deleted}.";
        return RedirectToPage();
    }

    private async Task LoadPageDataAsync(CancellationToken cancellationToken)
    {
        CurrentRetentionDays = await retentionService.GetRetentionDaysAsync(cancellationToken);
        ReadingCount = await db.MeterReadings.CountAsync(cancellationToken);
        OldestReadingReceivedAt = await db.MeterReadings
            .OrderBy(x => x.ReceivedAt)
            .Select(x => (DateTimeOffset?)x.ReceivedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public sealed class SettingsForm
    {
        [Range(1, 30, ErrorMessage = "Выберите срок хранения от 1 до 30 дней.")]
        public int RetentionDays { get; set; } = 1;
    }
}
