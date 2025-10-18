using Microsoft.EntityFrameworkCore;
using Pw.Modules.Api.Domain;

namespace Pw.Modules.Api.Data
{
    public class ModulesDbContext(DbContextOptions<ModulesDbContext> options) : DbContext(options)
    {
        public DbSet<Module> Modules => Set<Module>();
        public DbSet<UserModule> UserModules => Set<UserModule>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasPostgresExtension("uuid-ossp");

            modelBuilder.Entity<Module>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(x => x.Name).IsRequired().HasMaxLength(200);
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
                b.HasIndex(x => x.Name);
                b.HasMany(x => x.UserModules).WithOne(x => x.Module).HasForeignKey(x => x.ModuleId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<UserModule>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(x => x.UserId).IsRequired().HasMaxLength(200);
                b.HasIndex(x => new { x.UserId, x.ModuleId }).IsUnique();
            });
        }
    }
}