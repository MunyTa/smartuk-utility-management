using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using UkManagement.Web.Data;
using UkManagement.Web.Domain;
using UkManagement.Web.Services;
using ResidentEntity = UkManagement.Web.Domain.Resident;

namespace UkManagement.Web.Pages.Admin.Registrations;

public sealed class IndexModel(
    AppDbContext db,
    KeycloakAdminService keycloakAdmin,
    IEmailSender emailSender,
    AuditLogService auditLog,
    ILogger<IndexModel> logger) : PageModel
{
    public IReadOnlyList<ResidentRegistrationRequest> Requests { get; private set; } = [];

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadRequestsAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostApproveAsync(int id, CancellationToken cancellationToken)
    {
        var request = await db.RegistrationRequests.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (request is null)
        {
            return NotFound();
        }

        if (request.Status != RegistrationRequestStatus.PendingApproval)
        {
            StatusMessage = "Подтвердить можно только заявку со статусом ожидания решения УК.";
            return RedirectToPage();
        }

        var building = await db.Buildings.OrderBy(x => x.Id).FirstOrDefaultAsync(cancellationToken);
        if (building is null)
        {
            StatusMessage = "В системе не найден обслуживаемый дом.";
            return RedirectToPage();
        }

        if (await db.Residents.AnyAsync(x => x.KeycloakUsername == request.KeycloakUsername, cancellationToken))
        {
            StatusMessage = "Этот аккаунт уже привязан к жильцу.";
            return RedirectToPage();
        }

        var apartment = await db.Apartments.FirstOrDefaultAsync(
            x => x.BuildingId == building.Id && x.Number == request.ApartmentNumber,
            cancellationToken);
        if (apartment is null)
        {
            apartment = new Apartment
            {
                BuildingId = building.Id,
                Number = request.ApartmentNumber,
                Floor = InferFloor(request.ApartmentNumber)
            };
            db.Apartments.Add(apartment);
        }

        var resident = new ResidentEntity
        {
            FullName = request.FullName,
            Email = request.Email,
            Phone = request.Phone,
            KeycloakUsername = request.KeycloakUsername,
            Apartment = apartment
        };
        db.Residents.Add(resident);

        try
        {
            await keycloakAdmin.ApproveResidentAccountAsync(request.KeycloakUsername, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Resident registration approval failed for request {RequestId}", request.Id);
            StatusMessage = "Не удалось активировать аккаунт в Keycloak.";
            return RedirectToPage();
        }

        request.Status = RegistrationRequestStatus.Approved;
        request.ReviewedAt = DateTimeOffset.UtcNow;
        request.ReviewedBy = User.Identity?.Name;
        request.ReviewComment = "Заявка подтверждена.";

        await db.SaveChangesAsync(cancellationToken);
        await auditLog.LogAsync(
            User,
            "RegistrationApproved",
            "ResidentRegistrationRequest",
            $"Подтверждена регистрация {request.FullName}, кв. {request.ApartmentNumber}, email {request.Email}.",
            request.Id.ToString(),
            cancellationToken);
        await SendReviewEmailAsync(
            request.Email,
            "Регистрация SmartUK подтверждена",
            "Ваша заявка подтверждена. Теперь можно войти в SmartUK по email и паролю.",
            cancellationToken);

        StatusMessage = $"Регистрация {request.FullName} подтверждена.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRejectAsync(
        int id,
        string? reviewComment,
        CancellationToken cancellationToken)
    {
        var request = await db.RegistrationRequests.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (request is null)
        {
            return NotFound();
        }

        if (request.Status is RegistrationRequestStatus.Approved or RegistrationRequestStatus.Rejected)
        {
            StatusMessage = "Заявка уже обработана.";
            return RedirectToPage();
        }

        try
        {
            await keycloakAdmin.RejectResidentAccountAsync(request.KeycloakUsername, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Resident registration reject failed for request {RequestId}", request.Id);
            StatusMessage = "Не удалось отключить аккаунт в Keycloak.";
            return RedirectToPage();
        }

        request.Status = RegistrationRequestStatus.Rejected;
        request.ReviewedAt = DateTimeOffset.UtcNow;
        request.ReviewedBy = User.Identity?.Name;
        request.ReviewComment = string.IsNullOrWhiteSpace(reviewComment)
            ? "Заявка отклонена управляющей компанией."
            : reviewComment.Trim();

        await db.SaveChangesAsync(cancellationToken);
        await auditLog.LogAsync(
            User,
            "RegistrationRejected",
            "ResidentRegistrationRequest",
            $"Отклонена регистрация {request.FullName}, кв. {request.ApartmentNumber}, email {request.Email}.",
            request.Id.ToString(),
            cancellationToken);
        await SendReviewEmailAsync(
            request.Email,
            "Регистрация SmartUK отклонена",
            $"Ваша заявка отклонена. Комментарий: {request.ReviewComment}",
            cancellationToken);

        StatusMessage = $"Регистрация {request.FullName} отклонена.";
        return RedirectToPage();
    }

    private async Task LoadRequestsAsync(CancellationToken cancellationToken)
    {
        Requests = await db.RegistrationRequests
            .OrderBy(x => x.Status == RegistrationRequestStatus.PendingApproval ? 0 : 1)
            .ThenByDescending(x => x.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);
    }

    private async Task SendReviewEmailAsync(
        string email,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        try
        {
            await emailSender.SendAsync(email, subject, body, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Registration review email failed for {Email}", email);
        }
    }

    private static int InferFloor(string apartmentNumber)
    {
        var digits = new string(apartmentNumber.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var numericNumber)
            ? Math.Max(1, numericNumber / 10)
            : 1;
    }
}
