using Microsoft.EntityFrameworkCore;
using ProfilesApi.Models;

namespace ProfilesApi.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Profile> Profiles => Set<Profile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Profile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever(); // We generate UUID v7 manually
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Gender).HasMaxLength(50);
            entity.Property(e => e.AgeGroup).IsRequired().HasMaxLength(50);
            entity.Property(e => e.CountryId).IsRequired().HasMaxLength(10);
            entity.Property(e => e.CreatedAt).HasConversion(
                v => v,
                v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        });
    }
}
