using JobTrack.Application.Abstractions;
using JobTrack.Application.Common;
using JobTrack.Domain.Common;
using JobTrack.Domain.Entities;
using JobTrack.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace JobTrack.Infrastructure.Persistence;

public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options), IAppDbContext
{
    /// <summary>Unique index names mapped to user-facing conflicts (see <see cref="SaveChangesAsync(CancellationToken)"/>).</summary>
    private static readonly Dictionary<string, (string Code, string Message)> UniqueConflicts = new()
    {
        ["IX_Companies_UserId_NormalizedName"] =
            ("company.duplicate_name", "Ya tienes una empresa registrada con ese nombre."),
        ["IX_Applications_JobOfferId"] =
            ("application.duplicate", "Ya existe una postulación para esta oferta."),
    };

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<JobOffer> JobOffers => Set<JobOffer>();
    public DbSet<JobApplication> Applications => Set<JobApplication>();
    public DbSet<ApplicationStatusHistory> ApplicationStatusHistory => Set<ApplicationStatusHistory>();
    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();
    public DbSet<Reminder> Reminders => Set<Reminder>();
    public DbSet<Resume> Resumes => Set<Resume>();
    public DbSet<ResumeAnalysis> ResumeAnalyses => Set<ResumeAnalysis>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    /// <summary>
    /// Evaluated per query by EF Core's global filters. Anonymous contexts get <see cref="Guid.Empty"/>, so every
    /// owned set is empty for them (default deny).
    /// </summary>
    private Guid CurrentUserId => currentUser.UserId;

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampTimestamps();
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                                           {
                                               SqlState: PostgresErrorCodes.UniqueViolation,
                                           } pg &&
                                           pg.ConstraintName is not null &&
                                           UniqueConflicts.TryGetValue(pg.ConstraintName, out var conflict))
        {
            throw new ConflictException(conflict.Code, conflict.Message);
        }
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Row-level ownership. Child rows without their own UserId are filtered through their parent.
        builder.Entity<Company>().HasQueryFilter(e => e.UserId == CurrentUserId);
        builder.Entity<JobOffer>().HasQueryFilter(e => e.UserId == CurrentUserId);
        builder.Entity<JobApplication>().HasQueryFilter(e => e.UserId == CurrentUserId);
        builder.Entity<ApplicationStatusHistory>().HasQueryFilter(e => e.Application!.UserId == CurrentUserId);
        builder.Entity<CalendarEvent>().HasQueryFilter(e => e.UserId == CurrentUserId);
        builder.Entity<Reminder>().HasQueryFilter(e => e.UserId == CurrentUserId);
        builder.Entity<Resume>().HasQueryFilter(e => e.UserId == CurrentUserId);
        builder.Entity<ResumeAnalysis>().HasQueryFilter(e => e.UserId == CurrentUserId);
    }

    private void StampTimestamps()
    {
        var now = timeProvider.GetUtcNow();
        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = now;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    break;
            }
        }

        foreach (var entry in ChangeTracker.Entries<AppUser>().Where(e => e.State == EntityState.Added))
        {
            entry.Entity.CreatedAt = now;
        }
    }
}
