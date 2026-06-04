namespace UkManagement.Web.Domain;

public static class DisplayNames
{
    public static string ToDisplayName(this MeterType value) => value switch
    {
        MeterType.ColdWater => "Холодная вода",
        MeterType.HotWater => "Горячая вода",
        MeterType.Electricity => "Электроэнергия",
        MeterType.Gas => "Газ",
        MeterType.Heating => "Отопление",
        _ => value.ToString()
    };

    public static string ToDisplayName(this MeterStatus value) => value switch
    {
        MeterStatus.Online => "В сети",
        MeterStatus.Warning => "Требует внимания",
        MeterStatus.Offline => "Нет данных",
        _ => value.ToString()
    };

    public static string ToDisplayName(this ReadingQuality value) => value switch
    {
        ReadingQuality.Normal => "Норма",
        ReadingQuality.Anomaly => "Аномалия",
        ReadingQuality.Invalid => "Ошибка",
        _ => value.ToString()
    };

    public static string ToDisplayName(this NotificationChannel value) => value switch
    {
        NotificationChannel.Email => "Email",
        NotificationChannel.Sms => "SMS",
        NotificationChannel.Push => "Сообщение в профиле",
        _ => value.ToString()
    };

    public static string ToDisplayName(this NotificationStatus value) => value switch
    {
        NotificationStatus.Pending => "Ожидает отправки",
        NotificationStatus.Sent => "Отправлено",
        NotificationStatus.Failed => "Ошибка отправки",
        _ => value.ToString()
    };

    public static string ToDisplayName(this ServiceRequestCategory value) => value switch
    {
        ServiceRequestCategory.Plumbing => "Сантехника",
        ServiceRequestCategory.Electricity => "Электрика",
        ServiceRequestCategory.Cleaning => "Уборка",
        ServiceRequestCategory.Elevator => "Лифт",
        ServiceRequestCategory.Heating => "Отопление",
        ServiceRequestCategory.MeterReplacement => "Замена прибора учета",
        ServiceRequestCategory.MeterInstallation => "Добавление прибора учета",
        ServiceRequestCategory.Other => "Другое",
        _ => value.ToString()
    };

    public static string ToDisplayName(this ServiceRequestPriority value) => value switch
    {
        ServiceRequestPriority.Low => "Низкий",
        ServiceRequestPriority.Normal => "Обычный",
        ServiceRequestPriority.High => "Высокий",
        ServiceRequestPriority.Emergency => "Аварийный",
        _ => value.ToString()
    };

    public static string ToDisplayName(this ServiceRequestStatus value) => value switch
    {
        ServiceRequestStatus.New => "Новая",
        ServiceRequestStatus.InProgress => "В работе",
        ServiceRequestStatus.WaitingResident => "Ожидает жильца",
        ServiceRequestStatus.Completed => "Выполнена",
        ServiceRequestStatus.Cancelled => "Отменена",
        _ => value.ToString()
    };

    public static string ToDisplayName(this RegistrationRequestStatus value) => value switch
    {
        RegistrationRequestStatus.EmailCodeSent => "Ожидает подтверждения email",
        RegistrationRequestStatus.PendingApproval => "Ожидает решения УК",
        RegistrationRequestStatus.Approved => "Подтверждена",
        RegistrationRequestStatus.Rejected => "Отклонена",
        _ => value.ToString()
    };
}
