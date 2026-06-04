namespace UkManagement.Web.Domain;

public sealed class SystemSettings
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    public int MeterReadingRetentionDays { get; set; } = 1;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
