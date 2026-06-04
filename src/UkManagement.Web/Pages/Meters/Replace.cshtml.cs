using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using UkManagement.Web.Data;
using UkManagement.Web.Domain;

namespace UkManagement.Web.Pages.Meters;

[Authorize(Roles = "Admin")]
public sealed class ReplaceModel(
    AppDbContext db,
    UkManagement.Web.Services.AuditLogService auditLog) : PageModel
{
    public Meter Meter { get; private set; } = null!;

    [BindProperty]
    public ReplacementForm Form { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        var meter = await LoadMeterAsync(id, cancellationToken);
        if (meter is null)
        {
            return NotFound();
        }

        Meter = meter;
        Form.SerialNumber = meter.SerialNumber;
        Form.ExternalDeviceId = meter.ExternalDeviceId;
        Form.Unit = meter.Unit;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id, CancellationToken cancellationToken)
    {
        var meter = await LoadMeterAsync(id, cancellationToken);
        if (meter is null)
        {
            return NotFound();
        }

        Meter = meter;
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var externalDeviceId = Form.ExternalDeviceId.Trim();
        var duplicateDevice = await db.Meters.AnyAsync(
            x => x.Id != meter.Id && x.ExternalDeviceId == externalDeviceId,
            cancellationToken);
        if (duplicateDevice)
        {
            ModelState.AddModelError(nameof(Form.ExternalDeviceId), "Такой ID устройства уже используется другим прибором.");
            return Page();
        }

        var oldSerialNumber = meter.SerialNumber;
        var oldExternalDeviceId = meter.ExternalDeviceId;

        meter.SerialNumber = Form.SerialNumber.Trim();
        meter.ExternalDeviceId = externalDeviceId;
        meter.Unit = Form.Unit.Trim();
        meter.LastValue = null;
        meter.LastReadingAt = null;
        meter.Status = MeterStatus.Offline;

        await db.SaveChangesAsync(cancellationToken);
        await auditLog.LogAsync(
            User,
            "MeterReplaced",
            "Meter",
            $"Заменен прибор в кв. {meter.Apartment.Number}: {oldSerialNumber}/{oldExternalDeviceId} -> {meter.SerialNumber}/{meter.ExternalDeviceId}.",
            meter.Id.ToString(),
            cancellationToken);
        return RedirectToPage("/Meters/Index");
    }

    private async Task<Meter?> LoadMeterAsync(int id, CancellationToken cancellationToken)
    {
        return await db.Meters
            .Include(x => x.Apartment)
            .ThenInclude(x => x.Building)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public sealed class ReplacementForm
    {
        [Required(ErrorMessage = "Введите серийный номер.")]
        [StringLength(80, ErrorMessage = "Серийный номер не должен быть длиннее {1} символов.")]
        public string SerialNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введите ID устройства.")]
        [StringLength(120, ErrorMessage = "ID устройства не должен быть длиннее {1} символов.")]
        [RegularExpression("^[a-zA-Z0-9._-]{3,120}$", ErrorMessage = "ID устройства должен быть от 3 символов и может содержать латиницу, цифры, точку, дефис и нижнее подчеркивание.")]
        public string ExternalDeviceId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введите единицу измерения.")]
        [StringLength(24, ErrorMessage = "Единица измерения не должна быть длиннее {1} символов.")]
        public string Unit { get; set; } = string.Empty;
    }
}
