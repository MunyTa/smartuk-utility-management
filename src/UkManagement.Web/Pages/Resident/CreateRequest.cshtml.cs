using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using UkManagement.Web.Data;
using UkManagement.Web.Domain;
using UkManagement.Web.Services;
using ResidentEntity = UkManagement.Web.Domain.Resident;

namespace UkManagement.Web.Pages.ResidentArea;

public sealed class CreateRequestModel(
    AppDbContext db,
    CurrentResidentService currentResident,
    AuditLogService auditLog) : PageModel
{
    public ResidentEntity ResidentProfile { get; private set; } = null!;
    public IReadOnlyList<SelectListItem> CategoryOptions { get; private set; } = [];

    [BindProperty]
    public RequestForm Form { get; set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadPageDataAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        await LoadPageDataAsync(cancellationToken);
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var request = new ServiceRequest
        {
            ResidentId = ResidentProfile.Id,
            Category = Form.Category,
            Priority = Form.Category is ServiceRequestCategory.MeterReplacement or ServiceRequestCategory.MeterInstallation
                ? ServiceRequestPriority.High
                : ServiceRequestPriority.Normal,
            Status = ServiceRequestStatus.New,
            Title = Form.Title.Trim(),
            Description = Form.Description.Trim()
        };
        db.ServiceRequests.Add(request);

        await db.SaveChangesAsync(cancellationToken);
        await auditLog.LogAsync(
            User,
            "RequestCreated",
            "ServiceRequest",
            $"Жилец {ResidentProfile.FullName}, кв. {ResidentProfile.Apartment.Number}, создал заявку '{request.Title}'.",
            request.Id.ToString(),
            cancellationToken);
        return RedirectToPage("/Resident/Requests");
    }

    private async Task LoadPageDataAsync(CancellationToken cancellationToken)
    {
        ResidentProfile = await currentResident.GetRequiredAsync(User, cancellationToken);
        CategoryOptions = Enum.GetValues<ServiceRequestCategory>()
            .Select(x => new SelectListItem(x.ToDisplayName(), x.ToString()))
            .ToList();
    }

    public sealed class RequestForm
    {
        [Required(ErrorMessage = "Выберите категорию.")]
        public ServiceRequestCategory Category { get; set; } = ServiceRequestCategory.Other;

        [Required(ErrorMessage = "Введите тему заявки.")]
        [StringLength(180, ErrorMessage = "Тема не должна быть длиннее {1} символов.")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введите описание заявки.")]
        [StringLength(2000, ErrorMessage = "Описание не должно быть длиннее {1} символов.")]
        public string Description { get; set; } = string.Empty;
    }
}
