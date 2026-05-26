using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Compound_Sentry.Models;

namespace Compound_Sentry.Data
{
    public class ApplicationDbContext : IdentityDbContext<AdminUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Core entity DbSets
        public DbSet<DeviceEvent> DeviceEvents { get; set; }
        public DbSet<CurrentPresence> CurrentPresences { get; set; }

        // AdminUser is already included via IdentityDbContext<AdminUser>
        // But explicit declaration for clarity:
        public DbSet<AdminUser> AdminUsers { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ===== DeviceEvent Configuration =====
            modelBuilder.Entity<DeviceEvent>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.MacAddress).IsRequired().HasMaxLength(17);
                entity.Property(e => e.EventType).IsRequired().HasMaxLength(5);
                entity.Property(e => e.PhoneType).HasMaxLength(50);
                entity.HasIndex(e => e.EventTimestamp);
                entity.HasIndex(e => e.MacAddress);
            });

            // ===== CurrentPresence Configuration =====
            modelBuilder.Entity<CurrentPresence>(entity =>
            {
                entity.HasKey(e => e.MacAddress);
                entity.Property(e => e.MacAddress).HasMaxLength(17);
                entity.Property(e => e.PhoneType).HasMaxLength(50);
                entity.HasIndex(e => e.LastSeen);
            });

            // ===== AdminUser Configuration =====
            modelBuilder.Entity<AdminUser>(entity =>
            {
                // Personal info
                entity.Property(e => e.FullName).HasMaxLength(100);
                entity.Property(e => e.ProfilePictureUrl).HasMaxLength(500);
                entity.Property(e => e.PhoneNumber).HasMaxLength(20);

                // Account info
                entity.Property(e => e.Role).HasDefaultValue("Admin");

                // Indexes for performance
                entity.HasIndex(e => e.Email).IsUnique();
                entity.HasIndex(e => e.FullName);
                entity.HasIndex(e => e.IsActive);
                entity.HasIndex(e => e.CreatedAt);
            });
        }
    }
}