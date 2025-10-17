using Microsoft.EntityFrameworkCore;
using Pw.Hub.Models;

namespace Pw.Hub.Infrastructure;

public class AppDbContext : DbContext
{
    public DbSet<Squad> Squads { get; set; } = null!;
    public DbSet<Account> Accounts { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite("Data Source=pwhub.db");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Squad>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
            entity.HasMany(e => e.Accounts)
                .WithOne(e => e.Squad)
                .HasForeignKey(e => e.SquadId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.ImageSource).HasMaxLength(256);
        });

        base.OnModelCreating(modelBuilder);
    }
}
