using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using UkManagement.Web.Data;
using UkManagement.Web.Domain;
using UkManagement.Web.Services;

namespace UkManagement.Web.Pages.Meters;

public sealed class CreateModel(
    AppDbContext db,
    MeterProvisioningService meterProvisioning,
    AuditLogService auditLog) : PageModel
{
    public IReadOnlyList<SelectListItem> ApartmentOptions { get; private set; } = [];
    public IReadOnlyList<SelectListItem> TypeOptions { get; private set; } = [];

    [BindProperty]
    public MeterForm Form { get; set; } = new();

    public async Task OnGetAsync(int? apartmentId, CancellationToken cancellationToken)
    {
        await LoadOptionsAsync(cancellationToken);
        if (apartmentId is not null)
        {
            Form.ApartmentId = apartmentId.Value;
        }
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        await LoadOptionsAsync(cancellationToken);
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await meterProvisioning.CreateAsync(
            Form.ApartmentId,
            Form.Type,
            Form.SerialNumber,
            Form.ExternalDeviceId,
            Form.InitialValue,
            cancellationToken);

        if (!result.Succeeded || result.Meter is null)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Не удалось добавить прибор.");
            return Page();
        }

        await auditLog.LogAsync(
            User,
            "MeterCreated",
            "Meter",
            $"Добавлен прибор {result.Meter.SerialNumber}, ID устройства {result.Meter.ExternalDeviceId}.",
            result.Meter.Id.ToString(),
            cancellationToken);

        return RedirectToPage("/Meters/Index", new { apartmentId = Form.ApartmentId });
    }

    private async Task LoadOptionsAsync(CancellationToken cancellationToken)
    {
        ApartmentOptions = await db.Apartments
            .Include(x => x.Building)
            .OrderBy(x => x.Building.Address)
            .ThenBy(x => x.Number)
            .Select(x => new SelectListItem($"{x.Building.Address}, кв. {x.Number}", x.Id.ToString()))
            .ToListAsync(cancellationToken);

        TypeOptions = Enum.GetValues<MeterType>()
            .Select(x => new SelectListItem(x.ToDisplayName(), x.ToString()))
            .ToList();
    }

    public sealed class MeterForm
    {
        [Required(ErrorMessage = "Выберите квартиру.")]
        [Range(1, int.MaxValue, ErrorMessage = "Выберите квартиру.")]
        public int ApartmentId { get; set; }

        [Required(ErrorMessage = "Выберите тип прибора.")]
        public MeterType Type { get; set; } = MeterType.ColdWater;

        [Required(ErrorMessage = "Введите серийный номер.")]
        [StringLength(80, ErrorMessage = "Серийный номер не должен быть длиннее {1} символов.")]
        public string SerialNumber { get; set; } = string.Empty;

        [StringLength(120, ErrorMessage = "ID устройства не должен быть длиннее {1} символов.")]
        [RegularExpression("^[a-zA-Z0-9._-]{3,120}$", ErrorMessage = "ID устройства должен быть от 3 символов и может содержать латиницу, цифры, точку, дефис и нижнее подчеркивание.")]
        public string? ExternalDeviceId { get; set; }

        [Range(0, 999999999, ErrorMessage = "Начальное показание должно быть не меньше 0.")]
        public decimal? InitialValue { get; set; }
    }
}
