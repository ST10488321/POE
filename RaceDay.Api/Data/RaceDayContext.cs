using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

public class RaceDayContext(DbContextOptions<RaceDayContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Enrolment> Enrolments => Set<Enrolment>();
    public DbSet<Result> Results => Set<Result>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        });

        mb.Entity<Event>(e =>
        {
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.DistanceKm).HasPrecision(8, 2);
            e.HasOne(x => x.Organiser).WithMany().HasForeignKey(x => x.OrganiserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<Category>(e =>
            e.HasOne(c => c.Event).WithMany(x => x.Categories).HasForeignKey(c => c.EventId)
                .OnDelete(DeleteBehavior.Cascade));

        mb.Entity<Enrolment>(e =>
        {
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.Event).WithMany(x => x.Enrolments).HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Cascade);
            // Restrict avoids SQL Server "multiple cascade paths" errors.
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Participant).WithMany().HasForeignKey(x => x.ParticipantId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.ParticipantId, x.EventId }).IsUnique(); // one enrolment per event
        });

        mb.Entity<Result>(e =>
        {
            e.HasOne(r => r.Enrolment).WithOne(en => en.Result).HasForeignKey<Result>(r => r.EnrolmentId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(r => r.EnrolmentId).IsUnique();
        });
    }
}
