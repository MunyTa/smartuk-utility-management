using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using UkManagement.Web.Data;
using UkManagement.Web.Domain;

namespace UkManagement.Web.Pages.Requests;

public sealed class DetailsModel(
    AppDbContext db,
    UkManagement.Web.Services.AuditLogService auditLog,
    UkManagement.Web.Services.MeterProvisioningService meterProvisioning) : PageModel
{
    public ServiceRequest ServiceRequestItem { get; private set; } = null!;
    public IReadOnlyList<SelectListItem> StatusOptions { get; private set; } = [];
    public IReadOnlyList<SelectListItem> PriorityOptions { get; private set; } = [];
    public IReadOnlyList<SelectListItem> MeterTypeOptions { get; private set; } = [];
    public IReadOnlyList<SelectListItem> MeterOptions { get; private set; } = [];

    [BindProperty]
    public UpdateForm Form { get; set; } = new();

    [BindProperty]
    public MeterCreateForm MeterForm { get; set; } = new();

    [BindProperty]
    public MeterReplacementForm ReplacementForm { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        LoadOptions();
        var serviceRequest = await LoadServiceRequestAsync(id);
        if (serviceRequest is null)
        {
            return NotFound();
        }

        if (!CanAccess(serviceRequest))
        {
            return Forbid();
        }

        ServiceRequestItem = serviceRequest;
        Form.Status = serviceRequest.Status;
        Form.Priority = serviceRequest.Priority;
        Form.DispatcherComment = serviceRequest.DispatcherComment;
        await LoadMeterOptionsAsync(serviceRequest, CancellationToken.None);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id, CancellationToken cancellationToken)
    {
        LoadOptions();
        var serviceRequest = await LoadServiceRequestAsync(id, cancellationToken);
        if (serviceRequest is null)
        {
            return NotFound();
        }

        if (!CanAccess(serviceRequest))
        {
            return Forbid();
        }

        ServiceRequestItem = serviceRequest;
        await LoadMeterOptionsAsync(serviceRequest, cancellationToken);
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var oldStatus = serviceRequest.Status;
        var oldPriority = serviceRequest.Priority;

        serviceRequest.Status = Form.Status;
        serviceRequest.Priority = Form.Priority;
        serviceRequest.DispatcherComment = string.IsNullOrWhiteSpace(Form.DispatcherComment)
            ? null
            : Form.DispatcherComment.Trim();
        serviceRequest.UpdatedAt = DateTimeOffset.UtcNow;

        serviceRequest.ClosedAt = Form.Status is ServiceRequestStatus.Completed or ServiceRequestStatus.Cancelled
            ? serviceRequest.ClosedAt ?? DateTimeOffset.UtcNow
            : null;

        await db.SaveChangesAsync(cancellationToken);
        await auditLog.LogAsync(
            User,
            "RequestUpdated",
            "ServiceRequest",
            $"Заявка '{serviceRequest.Title}' обновлена: статус {oldStatus.ToDisplayName()} -> {Form.Status.ToDisplayName()}, приоритет {oldPriority.ToDisplayName()} -> {Form.Priority.ToDisplayName()}.",
            serviceRequest.Id.ToString(),
            cancellationToken);
        return RedirectToPage("/Requests/Details", new { id });
    }

    public async Task<IActionResult> OnPostCreateMeterAsync(int id, CancellationToken cancellationToken)
    {
        if (!User.IsInRole("Admin"))
        {
            return Forbid();
        }

        LoadOptions();
        var serviceRequest = await LoadServiceRequestAsync(id, cancellationToken);
        if (serviceRequest is null)
        {
            return NotFound();
        }

        if (!CanAccess(serviceRequest))
        {
            return Forbid();
        }

        ServiceRequestItem = serviceRequest;
        await LoadMeterOptionsAsync(serviceRequest, cancellationToken);
        ModelState.Clear();

        if (serviceRequest.Category != ServiceRequestCategory.MeterInstallation)
        {
            ModelState.AddModelError(string.Empty, "Добавить прибор можно только по заявке на добавление прибора учета.");
            return Page();
        }

        if (serviceRequest.Status is ServiceRequestStatus.Completed or ServiceRequestStatus.Cancelled)
        {
            ModelState.AddModelError(string.Empty, "Заявка уже закрыта.");
            return Page();
        }

        ValidateMeterForm();
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await meterProvisioning.CreateAsync(
            serviceRequest.Resident.ApartmentId,
            MeterForm.Type,
            MeterForm.SerialNumber,
            MeterForm.ExternalDeviceId,
            MeterForm.InitialValue,
            cancellationToken);

        if (!result.Succeeded || result.Meter is null)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Не удалось добавить прибор.");
            return Page();
        }

        serviceRequest.Status = ServiceRequestStatus.Completed;
        serviceRequest.UpdatedAt = DateTimeOffset.UtcNow;
        serviceRequest.ClosedAt = DateTimeOffset.UtcNow;
        serviceRequest.DispatcherComment = string.IsNullOrWhiteSpace(MeterForm.Comment)
            ? $"Добавлен прибор {result.Meter.SerialNumber}, ID устройства {result.Meter.ExternalDeviceId}."
            : MeterForm.Comment.Trim();

        await db.SaveChangesAsync(cancellationToken);
        await auditLog.LogAsync(
            User,
            "MeterCreatedFromRequest",
            "Meter",
            $"По заявке #{serviceRequest.Id} добавлен прибор {result.Meter.SerialNumber}, квартира {serviceRequest.Resident.Apartment.Number}.",
            result.Meter.Id.ToString(),
            cancellationToken);

        return RedirectToPage("/Requests/Details", new { id });
    }

    public async Task<IActionResult> OnPostReplaceMeterAsync(int id, CancellationToken cancellationToken)
    {
        if (!User.IsInRole("Admin"))
        {
            return Forbid();
        }

        LoadOptions();
        var serviceRequest = await LoadServiceRequestAsync(id, cancellationToken);
        if (serviceRequest is null)
        {
            return NotFound();
        }

        if (!CanAccess(serviceRequest))
        {
            return Forbid();
        }

        ServiceRequestItem = serviceRequest;
        await LoadMeterOptionsAsync(serviceRequest, cancellationToken);
        ModelState.Clear();

        if (serviceRequest.Category != ServiceRequestCategory.MeterReplacement)
        {
            ModelState.AddModelError(string.Empty, "Заменить прибор можно только по заявке на замену прибора учета.");
            return Page();
        }

        if (serviceRequest.Status is ServiceRequestStatus.Completed or ServiceRequestStatus.Cancelled)
        {
            ModelState.AddModelError(string.Empty, "Заявка уже закрыта.");
            return Page();
        }

        ValidateReplacementForm();
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var meter = await db.Meters
            .Include(x => x.Apartment)
            .FirstOrDefaultAsync(x => x.Id == ReplacementForm.MeterId, cancellationToken);
        if (meter is null || meter.ApartmentId != serviceRequest.Resident.ApartmentId)
        {
            ModelState.AddModelError(nameof(ReplacementForm.MeterId), "Выбранный прибор не относится к квартире жильца.");
            return Page();
        }

        var externalDeviceId = ReplacementForm.ExternalDeviceId.Trim();
        var duplicateDevice = await db.Meters.AnyAsync(
            x => x.Id != meter.Id && x.ExternalDeviceId == externalDeviceId,
            cancellationToken);
        if (duplicateDevice)
        {
            ModelState.AddModelError(nameof(ReplacementForm.ExternalDeviceId), "Такой ID устройства уже используется другим прибором.");
            return Page();
        }

        var oldSerialNumber = meter.SerialNumber;
        var oldExternalDeviceId = meter.ExternalDeviceId;

        meter.SerialNumber = ReplacementForm.SerialNumber.Trim();
        meter.ExternalDeviceId = externalDeviceId;
        meter.Unit = string.IsNullOrWhiteSpace(ReplacementForm.Unit)
            ? meter.Unit
            : ReplacementForm.Unit.Trim();
        meter.LastValue = ReplacementForm.InitialValue;
        meter.LastReadingAt = null;
        meter.Status = MeterStatus.Offline;

        serviceRequest.Status = ServiceRequestStatus.Completed;
        serviceRequest.UpdatedAt = DateTimeOffset.UtcNow;
        serviceRequest.ClosedAt = DateTimeOffset.UtcNow;
        serviceRequest.DispatcherComment = string.IsNullOrWhiteSpace(ReplacementForm.Comment)
            ? $"Заменен прибор {oldSerialNumber}/{oldExternalDeviceId} на {meter.SerialNumber}/{meter.ExternalDeviceId}."
            : ReplacementForm.Comment.Trim();

        await db.SaveChangesAsync(cancellationToken);
        await auditLog.LogAsync(
            User,
            "MeterReplacedFromRequest",
            "Meter",
            $"По заявке #{serviceRequest.Id} заменен прибор в кв. {serviceRequest.Resident.Apartment.Number}: {oldSerialNumber}/{oldExternalDeviceId} -> {meter.SerialNumber}/{meter.ExternalDeviceId}.",
            meter.Id.ToString(),
            cancellationToken);

        return RedirectToPage("/Requests/Details", new { id });
    }

    private async Task<ServiceRequest?> LoadServiceRequestAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return await db.ServiceRequests
            .Include(x => x.Resident)
            .ThenInclude(x => x.Apartment)
            .ThenInclude(x => x.Building)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    private bool CanAccess(ServiceRequest serviceRequest)
    {
        var isMeterRequest = serviceRequest.Category is ServiceRequestCategory.MeterInstallation
            or ServiceRequestCategory.MeterReplacement;

        return isMeterRequest
            ? User.IsInRole("Admin")
            : User.IsInRole("Dispatcher");
    }

    private void LoadOptions()
    {
        StatusOptions = Enum.GetValues<ServiceRequestStatus>()
            .Select(x => new SelectListItem(x.ToDisplayName(), x.ToString()))
            .ToList();
        PriorityOptions = Enum.GetValues<ServiceRequestPriority>()
            .Select(x => new SelectListItem(x.ToDisplayName(), x.ToString()))
            .ToList();
        MeterTypeOptions = Enum.GetValues<MeterType>()
            .Select(x => new SelectListItem(x.ToDisplayName(), x.ToString()))
            .ToList();
    }

    private async Task LoadMeterOptionsAsync(ServiceRequest serviceRequest, CancellationToken cancellationToken)
    {
        if (serviceRequest.Category != ServiceRequestCategory.MeterReplacement)
        {
            MeterOptions = [];
            return;
        }

        MeterOptions = await db.Meters
            .Where(x => x.ApartmentId == serviceRequest.Resident.ApartmentId)
            .OrderBy(x => x.Type)
            .ThenBy(x => x.SerialNumber)
            .Select(x => new SelectListItem(
                $"{x.Type.ToDisplayName()} - {x.SerialNumber} ({x.ExternalDeviceId})",
                x.Id.ToString()))
            .ToListAsync(cancellationToken);
    }

    private void ValidateMeterForm()
    {
        if (string.IsNullOrWhiteSpace(MeterForm.SerialNumber))
        {
            ModelState.AddModelError(nameof(MeterForm.SerialNumber), "Введите серийный номер прибора.");
        }

        if (!string.IsNullOrEmpty(MeterForm.SerialNumber) && MeterForm.SerialNumber.Length > 80)
        {
            ModelState.AddModelError(nameof(MeterForm.SerialNumber), "Серийный номер не должен быть длиннее 80 символов.");
        }

        if (!string.IsNullOrEmpty(MeterForm.ExternalDeviceId) && MeterForm.ExternalDeviceId.Length > 120)
        {
            ModelState.AddModelError(nameof(MeterForm.ExternalDeviceId), "ID устройства не должен быть длиннее 120 символов.");
        }

        if (!string.IsNullOrWhiteSpace(MeterForm.ExternalDeviceId)
            && !System.Text.RegularExpressions.Regex.IsMatch(MeterForm.ExternalDeviceId, "^[a-zA-Z0-9._-]{3,120}$"))
        {
            ModelState.AddModelError(nameof(MeterForm.ExternalDeviceId), "ID устройства должен быть от 3 символов и может содержать латиницу, цифры, точку, дефис и нижнее подчеркивание.");
        }

        if (MeterForm.InitialValue is < 0)
        {
            ModelState.AddModelError(nameof(MeterForm.InitialValue), "Начальное показание должно быть не меньше 0.");
        }
    }

    private void ValidateReplacementForm()
    {
        if (ReplacementForm.MeterId <= 0)
        {
            ModelState.AddModelError(nameof(ReplacementForm.MeterId), "Выберите прибор для замены.");
        }

        if (string.IsNullOrWhiteSpace(ReplacementForm.SerialNumber))
        {
            ModelState.AddModelError(nameof(ReplacementForm.SerialNumber), "Введите новый серийный номер.");
        }

        if (!string.IsNullOrEmpty(ReplacementForm.SerialNumber) && ReplacementForm.SerialNumber.Length > 80)
        {
            ModelState.AddModelError(nameof(ReplacementForm.SerialNumber), "Серийный номер не должен быть длиннее 80 символов.");
        }

        if (string.IsNullOrWhiteSpace(ReplacementForm.ExternalDeviceId))
        {
            ModelState.AddModelError(nameof(ReplacementForm.ExternalDeviceId), "Введите новый ID устройства.");
        }

        if (!string.IsNullOrEmpty(ReplacementForm.ExternalDeviceId) && ReplacementForm.ExternalDeviceId.Length > 120)
        {
            ModelState.AddModelError(nameof(ReplacementForm.ExternalDeviceId), "ID устройства не должен быть длиннее 120 символов.");
        }

        if (!string.IsNullOrWhiteSpace(ReplacementForm.ExternalDeviceId)
            && !System.Text.RegularExpressions.Regex.IsMatch(ReplacementForm.ExternalDeviceId, "^[a-zA-Z0-9._-]{3,120}$"))
        {
            ModelState.AddModelError(nameof(ReplacementForm.ExternalDeviceId), "ID устройства должен быть от 3 символов и может содержать латиницу, цифры, точку, дефис и нижнее подчеркивание.");
        }

        if (!string.IsNullOrEmpty(ReplacementForm.Unit) && ReplacementForm.Unit.Length > 24)
        {
            ModelState.AddModelError(nameof(ReplacementForm.Unit), "Единица измерения не должна быть длиннее 24 символов.");
        }

        if (ReplacementForm.InitialValue is < 0)
        {
            ModelState.AddModelError(nameof(ReplacementForm.InitialValue), "Начальное показание должно быть не меньше 0.");
        }
    }

    public sealed class UpdateForm
    {
        [Required(ErrorMessage = "Выберите статус.")]
        public ServiceRequestStatus Status { get; set; }

        [Required(ErrorMessage = "Выберите приоритет.")]
        public ServiceRequestPriority Priority { get; set; }

        [StringLength(1000, ErrorMessage = "Комментарий не должен быть длиннее {1} символов.")]
        public string? DispatcherComment { get; set; }
    }

    public sealed class MeterCreateForm
    {
        public MeterType Type { get; set; } = MeterType.ColdWater;

        public string SerialNumber { get; set; } = string.Empty;

        public string? ExternalDeviceId { get; set; }

        public decimal? InitialValue { get; set; }

        [StringLength(1000, ErrorMessage = "Комментарий не должен быть длиннее {1} символов.")]
        public string? Comment { get; set; }
    }

    public sealed class MeterReplacementForm
    {
        public int MeterId { get; set; }

        public string SerialNumber { get; set; } = string.Empty;

        public string ExternalDeviceId { get; set; } = string.Empty;

        public string? Unit { get; set; }

        public decimal? InitialValue { get; set; }

        [StringLength(1000, ErrorMessage = "Комментарий не должен быть длиннее {1} символов.")]
        public string? Comment { get; set; }
    }
}
