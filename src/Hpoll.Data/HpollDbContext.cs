namespace Hpoll.Data;

using Microsoft.EntityFrameworkCore;
using Hpoll.Data.Entities;

public class HpollDbContext : DbContext
{
    public HpollDbContext(DbContextOptions<HpollDbContext> options) : base(options) { }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Hub> Hubs => Set<Hub>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<DeviceReading> DeviceReadings => Set<DeviceReading>();
    public DbSet<PollingLog> PollingLogs => Set<PollingLog>();
    public DbSet<SystemInfo> SystemInfo => Set<SystemInfo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasIndex(e => e.Email);
        });

        modelBuilder.Entity<Hub>(entity =>
        {
            entity.HasIndex(e => e.HueBridgeId).IsUnique();
            entity.HasOne(e => e.Customer)
                .WithMany(c => c.Hubs)
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Device>(entity =>
        {
            entity.HasIndex(e => new { e.HubId, e.HueDeviceId }).IsUnique();
            entity.HasOne(e => e.Hub)
                .WithMany(h => h.Devices)
                .HasForeignKey(e => e.HubId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeviceReading>(entity =>
        {
            entity.HasIndex(e => new { e.DeviceId, e.Timestamp });
            entity.HasIndex(e => e.Timestamp);
            entity.HasOne(e => e.Device)
                .WithMany(d => d.Readings)
                .HasForeignKey(e => e.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PollingLog>(entity =>
        {
            entity.HasIndex(e => new { e.HubId, e.Timestamp });
            entity.HasOne(e => e.Hub)
                .WithMany(h => h.PollingLogs)
                .HasForeignKey(e => e.HubId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SystemInfo>(entity =>
        {
            entity.HasKey(e => e.Key);
            entity.Property(e => e.Key).HasMaxLength(128);
            entity.HasIndex(e => e.Category);
        });
    }
}
