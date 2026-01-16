using Microsoft.EntityFrameworkCore;
using NetSentry.Server.Models;

namespace NetSentry.Server.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Machine> Machines { get; set; }
    public DbSet<Metric> Metrics { get; set; }
    public DbSet<Disk> Disks { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Machine -> Metrics (1:N)
        modelBuilder.Entity<Metric>()
            .HasOne(m => m.Machine)
            .WithMany(x => x.Metrics)
            .HasForeignKey(m => m.MachineId);

        // Machine -> Disks (1:N)
        modelBuilder.Entity<Disk>()
            .HasOne(d => d.Machine)
            .WithMany(x => x.Disks)
            .HasForeignKey(d => d.MachineId);
    }
}
