using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using UkManagement.Web.Data;
using UkManagement.Web.Domain;
using UkManagement.Web.Services;

namespace UkManagement.Web.Pages.Notifications;

public sealed class CreateModel(
    AppDbContext db,
    NotificationService notificationService,
    AuditLogService auditLog) : PageModel
{
    public IReadOnlyList<SelectListItem> Apartments { get; private set; } = [];
    public IReadOnlyList<SelectListItem> ChannelOptions { get; private set; } = [];
    public IReadOnlyList<NotificationTemplate> Templates { get; private set; } = [];

    private static readonly IReadOnlyList<NotificationTemplate> DispatcherTemplates =
    [
        new("Отключение горячей воды", "Отключение горячей воды", "Уведомляем, что в доме будет временно отключена горячая вода. Ориентировочное время работ: ..."),
        new("Отключение холодной воды", "Отключение холодной воды", "Уведомляем, что в доме будет временно отключена холодная вода. Ориентировочное время работ: ..."),
        new("Плановые ремонтные работы", "Плановые ремонтные работы", "Уведомляем о проведении плановых ремонтных работ в доме. Просим учитывать возможные временные неудобства."),
        new("Аварийная ситуация", "Аварийная ситуация", "В доме зафиксирована аварийная ситуация. Управляющая компания уже выполняет необходимые работы."),
        new("Собрание жильцов", "Собрание жильцов", "Уведомляем о проведении собрания жильцов. Дата, время и место проведения: ...")
    ];

    private static readonly IReadOnlyList<NotificationTemplate> AdminTemplates =
    [
        new("Регистрация подтверждена", "Регистрация SmartUK подтверждена", "Ваша регистрация подтверждена. Теперь можно войти в личный кабинет SmartUK."),
        new("Регистрация отклонена", "Регистрация SmartUK отклонена", "Ваша заявка на регистрацию отклонена. Для уточнения обратитесь в управляющую компанию."),
        new("Изменение правил обслуживания", "Изменение правил обслуживания", "Уведомляем об изменении правил обслуживания дома. Подробности доступны у управляющей компании."),
        new("Финансовое уведомление", "Финансовое уведомление", "Уведомляем о финансовом вопросе по обслуживанию дома. Подробности: ...")
    ];

    [BindProperty]
    public NotificationForm Form { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        LoadChannelOptions();
        LoadTemplates();
        await LoadRecipientsAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        LoadChannelOptions();
        LoadTemplates();
        await LoadRecipientsAsync(cancellationToken);
        ValidateTargetSelection();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var target = await ResolveTargetAsync(cancellationToken);
        if (target.ResidentIds.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Для выбранного получателя не найдено жильцов.");
            return Page();
        }

        var notifications = new List<NotificationMessage>();
        foreach (var residentId in target.ResidentIds.Distinct())
        {
            notifications.Add(await notificationService.SendAsync(
                residentId,
                Form.Channel,
                Form.Subject,
                Form.Body,
                cancellationToken));
        }

        var sentCount = notifications.Count(x => x.Status == NotificationStatus.Sent);
        var failedCount = notifications.Count(x => x.Status == NotificationStatus.Failed);
        await auditLog.LogAsync(
            User,
            notifications.Count == 1 ? "NotificationSent" : "NotificationsSent",
            "Notification",
            $"Отправлено уведомление '{Form.Subject}' через {Form.Channel.ToDisplayName()}. Получатели: {target.Description}. Успешно: {sentCount}, ошибок: {failedCount}.",
            notifications.Count == 1 ? notifications[0].Id.ToString() : null,
            cancellationToken);

        StatusMessage = $"Уведомление отправлено. Получатели: {target.Description}. Успешно: {sentCount}, ошибок: {failedCount}.";
        return RedirectToPage("/Notifications/Index");
    }

    private async Task LoadRecipientsAsync(CancellationToken cancellationToken)
    {
        var apartments = await db.Apartments
            .Include(x => x.Building)
            .Include(x => x.Residents)
            .OrderBy(x => x.Building.Address)
            .ThenBy(x => x.Number)
            .ToListAsync(cancellationToken);

        Apartments = apartments
            .Select(x =>
            {
                var residents = x.Residents.Count == 0
                    ? "нет жильцов"
                    : string.Join(", ", x.Residents.OrderBy(r => r.FullName).Select(r => r.FullName));
                return new SelectListItem($"кв. {x.Number} - {residents}", x.Id.ToString());
            })
            .ToList();
    }

    private void LoadChannelOptions()
    {
        ChannelOptions = Enum.GetValues<NotificationChannel>()
            .Select(x => new SelectListItem(x.ToDisplayName(), x.ToString()))
            .ToList();
    }

    private void LoadTemplates()
    {
        Templates = User.IsInRole("Admin")
            ? AdminTemplates
            : DispatcherTemplates;
    }

    private void ValidateTargetSelection()
    {
        if (Form.TargetMode == NotificationTargetMode.Apartment && Form.ApartmentId is null)
        {
            ModelState.AddModelError(nameof(Form.ApartmentId), "Выберите квартиру.");
        }

    }

    private async Task<NotificationTarget> ResolveTargetAsync(CancellationToken cancellationToken)
    {
        if (Form.TargetMode == NotificationTargetMode.Apartment && Form.ApartmentId is not null)
        {
            var apartment = await db.Apartments
                .Include(x => x.Residents)
                .FirstOrDefaultAsync(x => x.Id == Form.ApartmentId.Value, cancellationToken);
            return apartment is null
                ? new NotificationTarget([], "квартира не найдена")
                : new NotificationTarget(
                    apartment.Residents.Select(x => x.Id).ToList(),
                    $"кв. {apartment.Number}");
        }

        var allResidentIds = await db.Residents
            .OrderBy(x => x.Apartment.Number)
            .ThenBy(x => x.FullName)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        return new NotificationTarget(allResidentIds, "все жильцы");
    }

    public sealed class NotificationForm
    {
        [Required(ErrorMessage = "Выберите получателей.")]
        public NotificationTargetMode TargetMode { get; set; } = NotificationTargetMode.Apartment;

        public int? ApartmentId { get; set; }

        [Required(ErrorMessage = "Выберите канал доставки.")]
        public NotificationChannel Channel { get; set; } = NotificationChannel.Email;

        [Required(ErrorMessage = "Введите тему уведомления.")]
        [StringLength(180, ErrorMessage = "Тема не должна быть длиннее {1} символов.")]
        public string Subject { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введите текст сообщения.")]
        [StringLength(2000, ErrorMessage = "Сообщение не должно быть длиннее {1} символов.")]
        public string Body { get; set; } = string.Empty;
    }

    public sealed record NotificationTemplate(string Name, string Subject, string Body);

    public sealed record NotificationTarget(IReadOnlyList<int> ResidentIds, string Description);

    public enum NotificationTargetMode
    {
        Apartment = 1,
        All = 2
    }
}
