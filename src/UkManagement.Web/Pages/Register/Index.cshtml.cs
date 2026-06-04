using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using UkManagement.Web.Data;
using UkManagement.Web.Domain;
using UkManagement.Web.Services;

namespace UkManagement.Web.Pages.Register;

public sealed class IndexModel(
    AppDbContext db,
    KeycloakAdminService keycloakAdmin,
    IEmailSender emailSender,
    ILogger<IndexModel> logger) : PageModel
{
    [BindProperty]
    public RegisterForm Form { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var email = Form.Email.Trim().ToLowerInvariant();
        var apartmentNumber = Form.ApartmentNumber.Trim();

        if (await db.Residents.AnyAsync(x => x.Email.ToLower() == email || x.KeycloakUsername == email, cancellationToken))
        {
            ModelState.AddModelError(nameof(Form.Email), "Этот email уже привязан к действующему жильцу.");
        }

        var existingRequest = await db.RegistrationRequests
            .Where(x => x.Email == email
                && x.Status != RegistrationRequestStatus.Approved
                && x.Status != RegistrationRequestStatus.Rejected)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var fullName = Form.FullName.Trim();
        var nameParts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var firstName = nameParts.Length > 1 ? nameParts[1] : fullName;
        var lastName = nameParts.Length > 0 ? nameParts[0] : fullName;
        var code = RandomNumberGenerator.GetInt32(10000, 100000).ToString();

        try
        {
            await keycloakAdmin.EnsurePendingResidentAccountAsync(
                email,
                email,
                firstName,
                lastName,
                Form.Password,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Pending Keycloak account creation failed for {Email}", email);
            ModelState.AddModelError(string.Empty, "Не удалось подготовить учетную запись. Попробуйте позже.");
            return Page();
        }

        var request = existingRequest ?? new ResidentRegistrationRequest
        {
            FullName = fullName,
            ApartmentNumber = apartmentNumber,
            Email = email,
            Phone = Form.Phone.Trim(),
            KeycloakUsername = email,
            VerificationCodeHash = ResidentRegistrationRequest.HashCode(code),
            VerificationCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15)
        };

        request.FullName = fullName;
        request.ApartmentNumber = apartmentNumber;
        request.Phone = Form.Phone.Trim();
        request.Status = RegistrationRequestStatus.EmailCodeSent;
        request.VerificationCodeHash = ResidentRegistrationRequest.HashCode(code);
        request.VerificationCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15);
        request.EmailVerifiedAt = null;
        request.ReviewedAt = null;
        request.ReviewedBy = null;
        request.ReviewComment = null;

        if (existingRequest is null)
        {
            db.RegistrationRequests.Add(request);
        }

        await db.SaveChangesAsync(cancellationToken);

        try
        {
            await emailSender.SendAsync(
                email,
                "Код подтверждения SmartUK",
                $"""
                Ваш код подтверждения регистрации: {code}

                Введите его на странице подтверждения SmartUK. Код действует 15 минут.
                """,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Registration verification email failed for {Email}", email);
            ModelState.AddModelError(string.Empty, "Заявка создана, но письмо с кодом не отправилось. Попробуйте регистрацию еще раз позже.");
            return Page();
        }

        return RedirectToPage("/Register/Confirm", new { id = request.Id });
    }

    public sealed class RegisterForm
    {
        [Required(ErrorMessage = "Введите ФИО.")]
        [StringLength(120, ErrorMessage = "ФИО не должно быть длиннее {1} символов.")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введите номер квартиры.")]
        [StringLength(24, ErrorMessage = "Номер квартиры не должен быть длиннее {1} символов.")]
        [RegularExpression("^[a-zA-Zа-яА-Я0-9./-]+$", ErrorMessage = "Номер квартиры может содержать буквы, цифры, точку, дефис и дробь.")]
        public string ApartmentNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введите email.")]
        [EmailAddress(ErrorMessage = "Введите корректный email.")]
        [StringLength(180, ErrorMessage = "Email не должен быть длиннее {1} символов.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введите телефон.")]
        [StringLength(32, ErrorMessage = "Телефон не должен быть длиннее {1} символов.")]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введите пароль.")]
        [StringLength(80, MinimumLength = 8, ErrorMessage = "Пароль должен быть от {2} до {1} символов.")]
        public string Password { get; set; } = string.Empty;

        [Compare(nameof(Password), ErrorMessage = "Пароли не совпадают.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
