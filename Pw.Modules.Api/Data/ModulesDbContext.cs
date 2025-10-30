using Microsoft.EntityFrameworkCore;
using Pw.Modules.Api.Domain;

namespace Pw.Modules.Api.Data
{
    public class ModulesDbContext(DbContextOptions<ModulesDbContext> options) : DbContext(options)
    {
        public DbSet<Module> Modules => Set<Module>();
        public DbSet<UserModule> UserModules => Set<UserModule>();
        public DbSet<User> Users => Set<User>();
        public DbSet<Session> Sessions => Set<Session>();
        public DbSet<TelegramLinkState> TelegramLinkStates => Set<TelegramLinkState>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasPostgresExtension("uuid-ossp");

            modelBuilder.Entity<Module>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(x => x.Name).IsRequired().HasMaxLength(200);
                b.Property(x => x.Version).IsRequired().HasMaxLength(50).HasDefaultValue("1.0.0");
                b.Property(x => x.Description);
                b.Property(x => x.Script)
                    .IsRequired();
                b.Property(x => x.InputsJson)
                    .HasColumnType("jsonb")
                    .HasDefaultValueSql("'[]'::jsonb")
                    .IsRequired();
                b.Property(x => x.RunCount).HasDefaultValue(0);
                b.Property(x => x.CreatedAt);
                b.Property(x => x.UpdatedAt);
                b.Property(x => x.OwnerUserId).HasMaxLength(100);
                b.HasIndex(x => x.Name);
                b.HasMany(x => x.UserModules).WithOne(x => x.Module).HasForeignKey(x => x.ModuleId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<UserModule>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(x => x.UserId).IsRequired().HasMaxLength(200);
                b.HasIndex(x => new { x.UserId, x.ModuleId }).IsUnique();
            });

            modelBuilder.Entity<User>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(x => x.Username).IsRequired().HasMaxLength(100);
                b.HasIndex(x => x.Username).IsUnique();
                b.Property(x => x.PasswordHash).IsRequired();
                b.Property(x => x.PasswordSalt).IsRequired();
                b.Property(x => x.Developer).HasDefaultValue(false);
                b.Property(x => x.CreatedAt);

                // Telegram link mapping
                b.Property(x => x.TelegramId);
                b.Property(x => x.TelegramUsername);
                b.Property(x => x.TelegramLinkedAt);
                b.HasIndex(x => x.TelegramId).IsUnique().HasFilter("\"TelegramId\" IS NOT NULL");
            });

            modelBuilder.Entity<Session>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(x => x.Token).IsRequired().HasMaxLength(200);
                b.HasIndex(x => x.Token).IsUnique();
                b.Property(x => x.UserId).IsRequired();
                b.HasOne(x => x.User).WithMany(u => u.Sessions).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
                b.Property(x => x.CreatedAt);
                b.Property(x => x.ExpiresAt);
            });

            modelBuilder.Entity<TelegramLinkState>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(x => x.State).IsRequired().HasMaxLength(200);
                b.HasIndex(x => x.State).IsUnique();
                b.Property(x => x.UserId).IsRequired();
                b.Property(x => x.CreatedAt);
                b.Property(x => x.ExpiresAt);
                b.Property(x => x.ConsumedAt);
            });
        }
    }
}