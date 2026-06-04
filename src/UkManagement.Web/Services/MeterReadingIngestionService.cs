using Microsoft.EntityFrameworkCore;
using UkManagement.Web.Data;
using UkManagement.Web.Domain;

namespace UkManagement.Web.Services;

public sealed class MeterReadingIngestionService(
    AppDbContext db,
    NotificationService notificationService,
    MeterReadingRetentionService retentionService,
    ILogger<MeterReadingIngestionService> logger)
{
    public async Task<IngestionResult> IngestAsync(
        MeterReadingPayload payload,
        CancellationToken cancellationToken = default)
    {
        if (payload.Value < 0)
        {
            return IngestionResult.Rejected("Показание не может быть отрицательным.");
        }

        var meter = await db.Meters
            .Include(x => x.Apartment)
            .ThenInclude(x => x.Residents)
            .FirstOrDefaultAsync(x => x.ExternalDeviceId == payload.DeviceId, cancellationToken);

        if (meter is null)
        {
            logger.LogWarning("Unknown meter device id received from MQTT: {DeviceId}", payload.DeviceId);
            return IngestionResult.Rejected($"Неизвестный идентификатор устройства '{payload.DeviceId}'.");
        }

        var duplicateExists = await db.MeterReadings
            .AnyAsync(x => x.MeterId == meter.Id && x.MeasuredAt == payload.MeasuredAt, cancellationToken);
        if (duplicateExists)
        {
            return IngestionResult.Stored(ReadingQuality.Normal, "Повторное показание было пропущено.");
        }

        var quality = ClassifyReading(meter, payload);

        var reading = new MeterReading
        {
            MeterId = meter.Id,
            Value = payload.Value,
            MeasuredAt = payload.MeasuredAt,
            ReceivedAt = DateTimeOffset.UtcNow,
            SignalRssi = payload.SignalRssi,
            BatteryVoltage = payload.BatteryVoltage,
            Quality = quality
        };

        meter.LastValue = payload.Value;
        meter.LastReadingAt = payload.MeasuredAt;
        meter.Status = quality == ReadingQuality.Normal ? MeterStatus.Online : MeterStatus.Warning;

        db.MeterReadings.Add(reading);
        await db.SaveChangesAsync(cancellationToken);
        await retentionService.DeleteExpiredReadingsAsync(cancellationToken);

        if (quality != ReadingQuality.Normal)
        {
            await NotifyAboutProblemAsync(meter, payload, quality, cancellationToken);
        }

        return IngestionResult.Stored(quality, $"Показание для {meter.ExternalDeviceId} сохранено.");
    }

    private static ReadingQuality ClassifyReading(Meter meter, MeterReadingPayload payload)
    {
        if (!string.Equals(meter.Unit, payload.Unit, StringComparison.OrdinalIgnoreCase))
        {
            return ReadingQuality.Invalid;
        }

        if (meter.LastValue is null)
        {
            return ReadingQuality.Normal;
        }

        var delta = payload.Value - meter.LastValue.Value;
        if (delta < 0)
        {
            return ReadingQuality.Invalid;
        }

        var elapsedHours = meter.LastReadingAt is null
            ? 1m
            : Math.Max(1m / 60m, (decimal)(payload.MeasuredAt - meter.LastReadingAt.Value).Duration().TotalHours);

        var (minimumJump, hourlyLimit) = meter.Type switch
        {
            MeterType.Electricity => (2m, 15m),
            MeterType.Heating => (0.05m, 0.25m),
            MeterType.Gas => (0.08m, 1.5m),
            _ => (0.05m, 0.8m)
        };
        var threshold = Math.Max(minimumJump, hourlyLimit * elapsedHours);

        return delta > threshold ? ReadingQuality.Anomaly : ReadingQuality.Normal;
    }

    private async Task NotifyAboutProblemAsync(
        Meter meter,
        MeterReadingPayload payload,
        ReadingQuality quality,
        CancellationToken cancellationToken)
    {
        var resident = meter.Apartment.Residents.FirstOrDefault();
        if (resident is null)
        {
            return;
        }

        var subject = quality == ReadingQuality.Invalid
            ? "Некорректные показания прибора учёта"
            : "Обнаружен резкий рост показаний";

        var body = $"""
            Прибор: {meter.SerialNumber}
            Квартира: {meter.Apartment.Number}
            Полученное значение: {payload.Value} {payload.Unit}
            Время измерения: {payload.MeasuredAt.ToDisplayTime():yyyy-MM-dd HH:mm:ss zzz}
            Статус: {quality.ToDisplayName()}
            """;

        await notificationService.SendAsync(resident.Id, NotificationChannel.Email, subject, body, cancellationToken);
    }
}
