using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UkManagement.Web.Data;
using UkManagement.Web.Domain;
using UkManagement.Web.Services;

namespace UkManagement.Tests;

public sealed class MeterReadingIngestionServiceTests
{
    [Fact]
    public async Task IngestAsync_StoresFirstNormalReading()
    {
        await using var db = CreateDbContext();
        await SeedAsync(db);
        var emailSender = new FakeEmailSender();
        var service = CreateService(db, emailSender);

        var result = await service.IngestAsync(new MeterReadingPayload
        {
            DeviceId = "meter-test-electricity",
            Unit = "kWh",
            Value = 120m,
            MeasuredAt = DateTimeOffset.UtcNow
        });

        var meter = await db.Meters.SingleAsync();
        Assert.True(result.Accepted);
        Assert.Equal(ReadingQuality.Normal, result.Quality);
        Assert.Equal(MeterStatus.Online, meter.Status);
        Assert.Empty(emailSender.Messages);
    }

    [Fact]
    public async Task IngestAsync_MarksLargeDeltaAsAnomalyAndSendsEmail()
    {
        await using var db = CreateDbContext();
        await SeedAsync(db, 120m);
        var emailSender = new FakeEmailSender();
        var service = CreateService(db, emailSender);

        var result = await service.IngestAsync(new MeterReadingPayload
        {
            DeviceId = "meter-test-electricity",
            Unit = "kWh",
            Value = 220m,
            MeasuredAt = DateTimeOffset.UtcNow.AddMinutes(1)
        });

        var notification = await db.Notifications.SingleAsync();
        Assert.True(result.Accepted);
        Assert.Equal(ReadingQuality.Anomaly, result.Quality);
        Assert.Equal(NotificationStatus.Sent, notification.Status);
        Assert.Single(emailSender.Messages);
    }

    [Fact]
    public async Task IngestAsync_DoesNotNotifyResidentWhenEmergencyNotificationsDisabled()
    {
        await using var db = CreateDbContext();
        await SeedAsync(db, 120m, resident =>
        {
            resident.EmergencyNotificationsEnabled = false;
        });
        var emailSender = new FakeEmailSender();
        var service = CreateService(db, emailSender);

        var result = await service.IngestAsync(new MeterReadingPayload
        {
            DeviceId = "meter-test-electricity",
            Unit = "kWh",
            Value = 220m,
            MeasuredAt = DateTimeOffset.UtcNow.AddMinutes(1)
        });

        Assert.True(result.Accepted);
        Assert.Equal(ReadingQuality.Anomaly, result.Quality);
        Assert.Empty(await db.Notifications.ToListAsync());
        Assert.Empty(emailSender.Messages);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new AppDbContext(options);
    }

    private static MeterReadingIngestionService CreateService(AppDbContext db, FakeEmailSender emailSender)
    {
        var pushNotifications = new PushNotificationService(
            db,
            Options.Create(new VapidOptions()),
            NullLogger<PushNotificationService>.Instance);
        var smsSender = new FakeSmsSender();
        var notifications = new NotificationService(
            db,
            emailSender,
            smsSender,
            pushNotifications,
            NullLogger<NotificationService>.Instance);
        var retention = new MeterReadingRetentionService(db, NullLogger<MeterReadingRetentionService>.Instance);
        return new MeterReadingIngestionService(
            db,
            notifications,
            retention,
            NullLogger<MeterReadingIngestionService>.Instance);
    }

    private static async Task SeedAsync(
        AppDbContext db,
        decimal? lastValue = null,
        Action<Resident>? configureResident = null)
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
        var resident = new Resident
        {
            FullName = "Test Resident",
            Email = "resident@example.local",
            Phone = "+79990000000"
        };
        configureResident?.Invoke(resident);
        apartment.Residents.Add(resident);
        apartment.Meters.Add(new Meter
        {
            SerialNumber = "TEST-EL",
            ExternalDeviceId = "meter-test-electricity",
            Type = MeterType.Electricity,
            Unit = "kWh",
            LastValue = lastValue,
            Status = lastValue is null ? MeterStatus.Offline : MeterStatus.Online,
            LastReadingAt = lastValue is null ? null : DateTimeOffset.UtcNow.AddMinutes(-5)
        });

        building.Apartments.Add(apartment);
        db.Buildings.Add(building);
        await db.SaveChangesAsync();
    }

    private sealed class FakeEmailSender : IEmailSender
    {
        public List<(string To, string Subject, string Body)> Messages { get; } = [];

        public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
        {
            Messages.Add((to, subject, body));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSmsSender : ISmsSender
    {
        public Task<string> SendAsync(string phone, string message, CancellationToken cancellationToken = default)
        {
            return Task.FromResult("SMS sent");
        }
    }
}
