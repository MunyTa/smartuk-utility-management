using Microsoft.EntityFrameworkCore;
using UkManagement.Web.Domain;

namespace UkManagement.Web.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<Apartment> Apartments => Set<Apartment>();
    public DbSet<Resident> Residents => Set<Resident>();
    public DbSet<Meter> Meters => Set<Meter>();
    public DbSet<MeterReading> MeterReadings => Set<MeterReading>();
    public DbSet<NotificationMessage> Notifications => Set<NotificationMessage>();
    public DbSet<ResidentPushSubscription> PushSubscriptions => Set<ResidentPushSubscription>();
    public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();
    public DbSet<SystemSettings> SystemSettings => Set<SystemSettings>();
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();
    public DbSet<ResidentRegistrationRequest> RegistrationRequests => Set<ResidentRegistrationRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Building>()
            .HasMany(x => x.Apartments)
            .WithOne(x => x.Building)
            .HasForeignKey(x => x.BuildingId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Apartment>()
            .HasMany(x => x.Residents)
            .WithOne(x => x.Apartment)
            .HasForeignKey(x => x.ApartmentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Apartment>()
            .HasIndex(x => new { x.BuildingId, x.Number })
            .IsUnique();

        modelBuilder.Entity<Apartment>()
            .HasMany(x => x.Meters)
            .WithOne(x => x.Apartment)
            .HasForeignKey(x => x.ApartmentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Meter>()
            .HasIndex(x => x.ExternalDeviceId)
            .IsUnique();

        modelBuilder.Entity<Meter>()
            .Property(x => x.LastValue)
            .HasPrecision(12, 3);

        modelBuilder.Entity<MeterReading>()
            .Property(x => x.Value)
            .HasPrecision(12, 3);

        modelBuilder.Entity<MeterReading>()
            .Property(x => x.BatteryVoltage)
            .HasPrecision(4, 2);

        modelBuilder.Entity<MeterReading>()
            .HasIndex(x => new { x.MeterId, x.MeasuredAt })
            .IsUnique();

        modelBuilder.Entity<NotificationMessage>()
            .HasOne(x => x.Resident)
            .WithMany(x => x.Notifications)
            .HasForeignKey(x => x.ResidentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<NotificationMessage>()
            .HasIndex(x => new { x.ResidentId, x.CreatedAt });

        modelBuilder.Entity<ResidentPushSubscription>()
            .HasOne(x => x.Resident)
            .WithMany(x => x.PushSubscriptions)
            .HasForeignKey(x => x.ResidentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ResidentPushSubscription>()
            .HasIndex(x => x.Endpoint)
            .IsUnique();

        modelBuilder.Entity<Resident>()
            .HasIndex(x => x.KeycloakUsername)
            .IsUnique()
            .HasFilter("\"KeycloakUsername\" IS NOT NULL");

        modelBuilder.Entity<ServiceRequest>()
            .HasOne(x => x.Resident)
            .WithMany(x => x.ServiceRequests)
            .HasForeignKey(x => x.ResidentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ServiceRequest>()
            .HasIndex(x => x.Status);

        modelBuilder.Entity<ServiceRequest>()
            .HasIndex(x => x.CreatedAt);

        modelBuilder.Entity<SystemSettings>()
            .Property(x => x.Id)
            .ValueGeneratedNever();

        modelBuilder.Entity<AuditLogEntry>()
            .HasIndex(x => x.CreatedAt);

        modelBuilder.Entity<AuditLogEntry>()
            .HasIndex(x => x.ActorUserName);

        modelBuilder.Entity<ResidentRegistrationRequest>()
            .HasIndex(x => x.Email);

        modelBuilder.Entity<ResidentRegistrationRequest>()
            .HasIndex(x => x.Status);
    }
}
