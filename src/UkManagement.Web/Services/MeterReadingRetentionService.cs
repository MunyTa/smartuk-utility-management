using Microsoft.EntityFrameworkCore;
using UkManagement.Web.Data;
using UkManagement.Web.Domain;

namespace UkManagement.Web.Services;

public sealed class MeterReadingRetentionService(
    AppDbContext db,
    ILogger<MeterReadingRetentionService> logger)
{
    public async Task<int> GetRetentionDaysAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetOrCreateSettingsAsync(cancellationToken);
        return settings.MeterReadingRetentionDays;
    }

    public async Task<int> UpdateRetentionDaysAsync(int days, CancellationToken cancellationToken = default)
    {
        ValidateRetentionDays(days);

        var settings = await GetOrCreateSettingsAsync(cancellationToken);
        settings.MeterReadingRetentionDays = days;
        settings.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return await DeleteExpiredReadingsAsync(cancellationToken);
    }

    public async Task<int> DeleteExpiredReadingsAsync(CancellationToken cancellationToken = default)
    {
        var retentionDays = await GetRetentionDaysAsync(cancellationToken);
        var cutoff = DateTimeOffset.UtcNow.AddDays(-retentionDays);
        var expiredReadings = db.MeterReadings.Where(x => x.ReceivedAt < cutoff);

        int deletedCount;
        if (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            var readings = await expiredReadings.ToListAsync(cancellationToken);
            deletedCount = readings.Count;
            db.MeterReadings.RemoveRange(readings);
            await db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            deletedCount = await expiredReadings.ExecuteDeleteAsync(cancellationToken);
        }

        if (deletedCount > 0)
        {
            logger.LogInformation("Deleted {Count} expired meter readings older than {Cutoff}.", deletedCount, cutoff);
        }

        return deletedCount;
    }

    private async Task<SystemSettings> GetOrCreateSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = await db.SystemSettings
            .FirstOrDefaultAsync(x => x.Id == SystemSettings.SingletonId, cancellationToken);
        if (settings is not null)
        {
            return settings;
        }

        settings = new SystemSettings();
        db.SystemSettings.Add(settings);
        await db.SaveChangesAsync(cancellationToken);
        return settings;
    }

    private static void ValidateRetentionDays(int days)
    {
        if (days is < 1 or > 30)
        {
            throw new ArgumentOutOfRangeException(nameof(days), "Срок хранения показаний должен быть от 1 до 30 дней.");
        }
    }
}
