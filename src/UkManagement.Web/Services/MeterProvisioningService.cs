using Microsoft.EntityFrameworkCore;
using UkManagement.Web.Data;
using UkManagement.Web.Domain;

namespace UkManagement.Web.Services;

public sealed class MeterProvisioningService(AppDbContext db)
{
    public async Task<MeterProvisioningResult> CreateAsync(
        int apartmentId,
        MeterType type,
        string serialNumber,
        string? externalDeviceId,
        decimal? initialValue,
        CancellationToken cancellationToken)
    {
        var apartment = await db.Apartments.FirstOrDefaultAsync(x => x.Id == apartmentId, cancellationToken);
        if (apartment is null)
        {
            return MeterProvisioningResult.Fail("Квартира не найдена.");
        }

        serialNumber = serialNumber.Trim();
        if (await db.Meters.AnyAsync(x => x.SerialNumber == serialNumber, cancellationToken))
        {
            return MeterProvisioningResult.Fail("Прибор с таким серийным номером уже есть.");
        }

        var deviceId = string.IsNullOrWhiteSpace(externalDeviceId)
            ? await BuildUniqueDeviceIdAsync(apartment.Number, type, cancellationToken)
            : externalDeviceId.Trim();

        if (await db.Meters.AnyAsync(x => x.ExternalDeviceId == deviceId, cancellationToken))
        {
            return MeterProvisioningResult.Fail("Прибор с таким ID устройства уже есть.");
        }

        var meter = new Meter
        {
            ApartmentId = apartment.Id,
            SerialNumber = serialNumber,
            ExternalDeviceId = deviceId,
            Type = type,
            Unit = UnitFor(type),
            Status = MeterStatus.Offline,
            LastValue = initialValue
        };

        db.Meters.Add(meter);
        await db.SaveChangesAsync(cancellationToken);
        return MeterProvisioningResult.Success(meter);
    }

    public static string UnitFor(MeterType type) => type switch
    {
        MeterType.Electricity => "kWh",
        MeterType.Heating => "Gcal",
        _ => "m3"
    };

    private async Task<string> BuildUniqueDeviceIdAsync(
        string apartmentNumber,
        MeterType type,
        CancellationToken cancellationToken)
    {
        var baseDeviceId = $"meter-{NormalizeDeviceSuffix(apartmentNumber)}-{TypeSuffix(type)}";
        var deviceId = baseDeviceId;
        var counter = 2;

        while (await db.Meters.AnyAsync(x => x.ExternalDeviceId == deviceId, cancellationToken))
        {
            deviceId = $"{baseDeviceId}-{counter}";
            counter++;
        }

        return deviceId;
    }

    private static string NormalizeDeviceSuffix(string apartmentNumber)
    {
        var safeChars = apartmentNumber
            .Trim()
            .ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray();
        var suffix = new string(safeChars).Trim('-');
        return string.IsNullOrWhiteSpace(suffix) ? "apartment" : suffix;
    }

    private static string TypeSuffix(MeterType type) => type switch
    {
        MeterType.ColdWater => "cold-water",
        MeterType.HotWater => "hot-water",
        MeterType.Electricity => "electricity",
        MeterType.Gas => "gas",
        MeterType.Heating => "heating",
        _ => "meter"
    };
}

public sealed record MeterProvisioningResult(bool Succeeded, string? Error, Meter? Meter)
{
    public static MeterProvisioningResult Success(Meter meter) => new(true, null, meter);

    public static MeterProvisioningResult Fail(string error) => new(false, error, null);
}
