using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using UkManagement.Web.Data;
using UkManagement.Web.Domain;
using UkManagement.Web.Services;
using ResidentEntity = UkManagement.Web.Domain.Resident;

namespace UkManagement.Web.Pages.ResidentArea;

public sealed class ReadingsModel(
    AppDbContext db,
    CurrentResidentService currentResident,
    MeterReadingIngestionService ingestionService,
    AuditLogService auditLog) : PageModel
{
    public ResidentEntity ResidentProfile { get; private set; } = null!;
    public IReadOnlyList<SelectListItem> MeterOptions { get; private set; } = [];
    public IReadOnlyList<MeterReading> Readings { get; private set; } = [];

    [TempData]
    public string? StatusMessage { get; set; }

    [BindProperty]
    public ReadingForm Form { get; set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadPageDataAsync(cancellationToken);
        Form.MeasuredAt = DisplayTime.Now;
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        await LoadPageDataAsync(cancellationToken);
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var meter = await db.Meters
            .FirstOrDefaultAsync(x => x.Id == Form.MeterId && x.ApartmentId == ResidentProfile.ApartmentId, cancellationToken);
        if (meter is null)
        {
            ModelState.AddModelError(nameof(Form.MeterId), "Выбранный прибор не найден в вашей квартире.");
            return Page();
        }

        var result = await ingestionService.IngestAsync(new MeterReadingPayload
        {
            DeviceId = meter.ExternalDeviceId,
            Unit = meter.Unit,
            Value = Form.Value,
            MeasuredAt = Form.MeasuredAt
        }, cancellationToken);

        StatusMessage = result.Accepted
            ? $"Показание сохранено. Качество: {result.Quality?.ToDisplayName() ?? "не определено"}."
            : result.Message;
        await auditLog.LogAsync(
            User,
            "ReadingSubmitted",
            "MeterReading",
            $"Жилец {ResidentProfile.FullName}, кв. {ResidentProfile.Apartment.Number}, передал показание {Form.Value} {meter.Unit} по прибору {meter.SerialNumber}.",
            meter.Id.ToString(),
            cancellationToken);

        return RedirectToPage("/Resident/Readings");
    }

    private async Task LoadPageDataAsync(CancellationToken cancellationToken)
    {
        ResidentProfile = await currentResident.GetRequiredAsync(User, cancellationToken);

        var meters = await db.Meters
            .Where(x => x.ApartmentId == ResidentProfile.ApartmentId)
            .OrderBy(x => x.Type)
            .ToListAsync(cancellationToken);

        MeterOptions = meters
            .Select(x => new SelectListItem($"{x.Type.ToDisplayName()} - {x.SerialNumber}", x.Id.ToString()))
            .ToList();

        var meterIds = meters.Select(x => x.Id).ToArray();
        Readings = await db.MeterReadings
            .Include(x => x.Meter)
            .Where(x => meterIds.Contains(x.MeterId))
            .OrderByDescending(x => x.MeasuredAt)
            .Take(50)
            .ToListAsync(cancellationToken);
    }

    public sealed class ReadingForm
    {
        [Required(ErrorMessage = "Выберите прибор учета.")]
        public int MeterId { get; set; }

        [Range(0, 1_000_000, ErrorMessage = "Показание должно быть неотрицательным.")]
        public decimal Value { get; set; }

        [Required(ErrorMessage = "Укажите время измерения.")]
        public DateTimeOffset MeasuredAt { get; set; }
    }
}
