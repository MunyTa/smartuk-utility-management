using Microsoft.EntityFrameworkCore;
using UkManagement.Web.Domain;

namespace UkManagement.Web.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.EnsureCreatedAsync();
        await EnsureResidentIdentityColumnsAsync(db);
        await EnsureNotificationReadColumnsAsync(db);
        await EnsurePushSubscriptionsTableAsync(db);
        await EnsureServiceRequestsTableAsync(db);
        await EnsureSystemSettingsTableAsync(db);
        await EnsureAuditLogTableAsync(db);
        await EnsureRegistrationRequestsTableAsync(db);
        await EnsureApartmentNumberIndexAsync(db);
        await EnsureSystemSettingsSeedAsync(db);

        if (await db.Meters.AnyAsync())
        {
            await EnsureResidentIdentitySeedAsync(db);
            await EnsureServiceRequestsSeedAsync(db);
            return;
        }

        const string seedAddress = "г. Москва, ул. Академическая, 16";
        var building = await db.Buildings.FirstOrDefaultAsync(x => x.Address == seedAddress)
            ?? new Building
            {
                Address = seedAddress,
                ManagementDistrict = "Центральный"
            };

        var apartment101 = new Apartment { Number = "101", Floor = 10, Building = building };
        var apartment42 = new Apartment { Number = "42", Floor = 4, Building = building };

        apartment101.Residents.Add(new Resident
        {
            FullName = "Иванов Сергей Петрович",
            Email = "ivanov101@example.local",
            Phone = "+79990001016",
            KeycloakUsername = "resident101"
        });

        apartment42.Residents.Add(new Resident
        {
            FullName = "Смирнова Анна Викторовна",
            Email = "smirnova42@example.local",
            Phone = "+79990000042",
            KeycloakUsername = "resident42"
        });

        apartment101.Meters.Add(new Meter
        {
            SerialNumber = "CW-101-2026",
            ExternalDeviceId = "meter-101-cold-water",
            Type = MeterType.ColdWater,
            Unit = "m3",
            Status = MeterStatus.Offline
        });
        apartment101.Meters.Add(new Meter
        {
            SerialNumber = "EL-101-2026",
            ExternalDeviceId = "meter-101-electricity",
            Type = MeterType.Electricity,
            Unit = "kWh",
            Status = MeterStatus.Offline
        });
        apartment42.Meters.Add(new Meter
        {
            SerialNumber = "HW-42-2026",
            ExternalDeviceId = "meter-42-hot-water",
            Type = MeterType.HotWater,
            Unit = "m3",
            Status = MeterStatus.Offline
        });
        apartment42.Meters.Add(new Meter
        {
            SerialNumber = "HT-42-2026",
            ExternalDeviceId = "meter-42-heating",
            Type = MeterType.Heating,
            Unit = "Gcal",
            Status = MeterStatus.Offline
        });

        building.Apartments.Add(apartment101);
        building.Apartments.Add(apartment42);

        if (building.Id == 0)
        {
            db.Buildings.Add(building);
        }

        await db.SaveChangesAsync();
        await EnsureResidentIdentitySeedAsync(db);
        await EnsureServiceRequestsSeedAsync(db);
    }

    private static async Task EnsureResidentIdentityColumnsAsync(AppDbContext db)
    {
        if (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            return;
        }

        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE "Residents"
                ADD COLUMN IF NOT EXISTS "KeycloakUsername" character varying(80) NULL;
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Residents_KeycloakUsername"
                ON "Residents" ("KeycloakUsername")
                WHERE "KeycloakUsername" IS NOT NULL;
            """);
    }

    private static async Task EnsureResidentIdentitySeedAsync(AppDbContext db)
    {
        var residents = await db.Residents
            .OrderBy(x => x.Id)
            .Take(2)
            .ToListAsync();

        if (residents.Count > 0 && string.IsNullOrWhiteSpace(residents[0].KeycloakUsername))
        {
            residents[0].KeycloakUsername = "resident101";
        }

        if (residents.Count > 1 && string.IsNullOrWhiteSpace(residents[1].KeycloakUsername))
        {
            residents[1].KeycloakUsername = "resident42";
        }

        await db.SaveChangesAsync();
    }

    private static async Task EnsureNotificationReadColumnsAsync(AppDbContext db)
    {
        if (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            return;
        }

        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE "Notifications"
                ADD COLUMN IF NOT EXISTS "ReadAt" timestamp with time zone NULL;
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_Notifications_ResidentId_CreatedAt"
                ON "Notifications" ("ResidentId", "CreatedAt");
            """);
    }

    private static async Task EnsurePushSubscriptionsTableAsync(AppDbContext db)
    {
        if (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            return;
        }

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "PushSubscriptions" (
                "Id" bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                "ResidentId" integer NOT NULL,
                "Endpoint" character varying(2048) NOT NULL,
                "P256Dh" character varying(256) NOT NULL,
                "Auth" character varying(128) NOT NULL,
                "UserAgent" character varying(300) NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "LastSeenAt" timestamp with time zone NOT NULL,
                CONSTRAINT "FK_PushSubscriptions_Residents_ResidentId"
                    FOREIGN KEY ("ResidentId") REFERENCES "Residents" ("Id") ON DELETE CASCADE
            );
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_PushSubscriptions_Endpoint"
                ON "PushSubscriptions" ("Endpoint");
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_PushSubscriptions_ResidentId"
                ON "PushSubscriptions" ("ResidentId");
            """);
    }

    private static async Task EnsureServiceRequestsTableAsync(AppDbContext db)
    {
        if (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            return;
        }

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "ServiceRequests" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                "ResidentId" integer NOT NULL,
                "Category" integer NOT NULL,
                "Priority" integer NOT NULL,
                "Status" integer NOT NULL,
                "Title" character varying(180) NOT NULL,
                "Description" character varying(2000) NOT NULL,
                "DispatcherComment" character varying(1000) NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NULL,
                "ClosedAt" timestamp with time zone NULL,
                CONSTRAINT "FK_ServiceRequests_Residents_ResidentId"
                    FOREIGN KEY ("ResidentId") REFERENCES "Residents" ("Id") ON DELETE CASCADE
            );
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_ServiceRequests_ResidentId"
                ON "ServiceRequests" ("ResidentId");
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_ServiceRequests_Status"
                ON "ServiceRequests" ("Status");
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_ServiceRequests_CreatedAt"
                ON "ServiceRequests" ("CreatedAt");
            """);
    }

    private static async Task EnsureSystemSettingsTableAsync(AppDbContext db)
    {
        if (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            return;
        }

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "SystemSettings" (
                "Id" integer NOT NULL PRIMARY KEY,
                "MeterReadingRetentionDays" integer NOT NULL,
                "UpdatedAt" timestamp with time zone NOT NULL
            );
            """);
    }

    private static async Task EnsureAuditLogTableAsync(AppDbContext db)
    {
        if (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            return;
        }

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "AuditLogEntries" (
                "Id" bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                "ActorUserName" character varying(80) NOT NULL,
                "ActorRole" character varying(40) NOT NULL,
                "ActionType" character varying(80) NOT NULL,
                "EntityName" character varying(180) NOT NULL,
                "EntityId" character varying(80) NULL,
                "Details" character varying(1000) NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL
            );
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_AuditLogEntries_CreatedAt"
                ON "AuditLogEntries" ("CreatedAt");
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_AuditLogEntries_ActorUserName"
                ON "AuditLogEntries" ("ActorUserName");
            """);
    }

    private static async Task EnsureRegistrationRequestsTableAsync(AppDbContext db)
    {
        if (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            return;
        }

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "RegistrationRequests" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                "FullName" character varying(120) NOT NULL,
                "ApartmentNumber" character varying(24) NOT NULL,
                "Email" character varying(180) NOT NULL,
                "Phone" character varying(32) NOT NULL,
                "KeycloakUsername" character varying(180) NOT NULL,
                "Status" integer NOT NULL,
                "VerificationCodeHash" character varying(128) NOT NULL,
                "VerificationCodeExpiresAt" timestamp with time zone NOT NULL,
                "EmailVerifiedAt" timestamp with time zone NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "ReviewedAt" timestamp with time zone NULL,
                "ReviewedBy" character varying(80) NULL,
                "ReviewComment" character varying(500) NULL
            );
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_RegistrationRequests_Email"
                ON "RegistrationRequests" ("Email");
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_RegistrationRequests_Status"
                ON "RegistrationRequests" ("Status");
            """);
    }

    private static async Task EnsureApartmentNumberIndexAsync(AppDbContext db)
    {
        if (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            return;
        }

        await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Apartments_BuildingId_Number"
                ON "Apartments" ("BuildingId", "Number");
            """);
    }

    private static async Task EnsureSystemSettingsSeedAsync(AppDbContext db)
    {
        if (!await db.SystemSettings.AnyAsync(x => x.Id == SystemSettings.SingletonId))
        {
            db.SystemSettings.Add(new SystemSettings());
            await db.SaveChangesAsync();
        }
    }

    private static async Task EnsureServiceRequestsSeedAsync(AppDbContext db)
    {
        if (await db.ServiceRequests.AnyAsync())
        {
            return;
        }

        var residents = await db.Residents
            .OrderBy(x => x.Id)
            .Take(2)
            .ToListAsync();
        if (residents.Count == 0)
        {
            return;
        }

        db.ServiceRequests.Add(new ServiceRequest
        {
            ResidentId = residents[0].Id,
            Category = ServiceRequestCategory.Plumbing,
            Priority = ServiceRequestPriority.High,
            Status = ServiceRequestStatus.InProgress,
            Title = "Протечка под раковиной",
            Description = "Житель сообщил о протечке в кухне. Требуется направить сантехника.",
            DispatcherComment = "Заявка принята, мастер назначен на сегодня.",
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-5),
            UpdatedAt = DateTimeOffset.UtcNow.AddHours(-4)
        });

        if (residents.Count > 1)
        {
            db.ServiceRequests.Add(new ServiceRequest
            {
                ResidentId = residents[1].Id,
                Category = ServiceRequestCategory.Elevator,
                Priority = ServiceRequestPriority.Emergency,
                Status = ServiceRequestStatus.New,
                Title = "Лифт не открывает двери на 4 этаже",
                Description = "Житель просит проверить работу лифта и передать заявку подрядчику.",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-40)
            });
        }

        await db.SaveChangesAsync();
    }
}
