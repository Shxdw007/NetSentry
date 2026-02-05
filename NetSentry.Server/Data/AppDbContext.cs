using Microsoft.EntityFrameworkCore;
using NetSentry.Server.Models;

namespace NetSentry.Server.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Machine> Machines { get; set; }
        public DbSet<Metric> Metrics { get; set; }
        public DbSet<Disk> Disks { get; set; }
        public DbSet<EventLog> EventLogs { get; set; }
        public DbSet<User> Users { get; set; } // Добавили таблицу пользователей

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique(); // Уникальные логины
        }
    }
}