using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using UkManagement.Web.Data;
using UkManagement.Web.Domain;
using UkManagement.Web.Services;

namespace UkManagement.Tests;

public sealed class MeterReadingRetentionServiceTests
{
    [Fact]
    public async Task UpdateRetentionDaysAsync_DeletesExpiredReadings()
    {
        await using var db = CreateDbContext();
        var meter = await SeedMeterAsync(db);
        db.MeterReadings.AddRange(
            new MeterReading
            {
                MeterId = meter.Id,
                Value = 10m,
                MeasuredAt = DateTimeOffset.UtcNow.AddDays(-3),
                ReceivedAt = DateTimeOffset.UtcNow.AddDays(-3)
            },
            new MeterReading
            {
                MeterId = meter.Id,
                Value = 11m,
                MeasuredAt = DateTimeOffset.UtcNow,
                ReceivedAt = DateTimeOffset.UtcNow
            });
        await db.SaveChangesAsync();

        var service = new MeterReadingRetentionService(db, NullLogger<MeterReadingRetentionService>.Instance);

        var deleted = await service.UpdateRetentionDaysAsync(1);

        Assert.Equal(1, deleted);
        Assert.Single(await db.MeterReadings.ToListAsync());
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<Meter> SeedMeterAsync(AppDbContext db)
    {
        var building = new Building
        {
            Address = "Test address",
            ManagementDistrict = "Test district"
        };
        var apartment = new Apartment
        {
            Number = "1",
            Floor = 1,
            Building = building
        };
        var meter = new Meter
        {
            SerialNumber = "TEST-1",
            ExternalDeviceId = "test-1",
            Type = MeterType.ColdWater,
            Unit = "m3",
            Apartment = apartment
        };
        apartment.Meters.Add(meter);
        building.Apartments.Add(apartment);
        db.Buildings.Add(building);
        await db.SaveChangesAsync();
        return meter;
    }
}
