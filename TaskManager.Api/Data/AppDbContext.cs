using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TaskManager.Api.Models;

namespace TaskManager.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite materializes DateTime with Kind=Unspecified; we only ever store UTC,
        // so restore the Kind on read to keep JSON output correctly suffixed with 'Z'.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(user =>
        {
            user.Property(u => u.Email).HasMaxLength(256);
            user.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<TaskItem>(task =>
        {
            task.Property(t => t.Title).HasMaxLength(200);
            task.Property(t => t.Description).HasMaxLength(2000);
            // FK without navigation properties - the app only ever queries by UserId.
            // EF adds the index and cascade delete by convention.
            task.HasOne<User>().WithMany().HasForeignKey(t => t.UserId);
        });
    }

    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        v => v,
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
}
