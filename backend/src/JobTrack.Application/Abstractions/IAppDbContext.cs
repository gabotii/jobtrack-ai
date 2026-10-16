using JobTrack.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JobTrack.Application.Abstractions;

/// <summary>
/// Persistence boundary used by the use cases. EF Core's DbSet already is a repository/unit of work, so we expose it
/// directly instead of wrapping it in pass-through repositories. Every set is filtered to the current user's rows by
/// global query filters in the implementation (default deny when there is no authenticated user).
/// </summary>
public interface IAppDbContext
{
    DbSet<Company> Companies { get; }
    DbSet<JobOffer> JobOffers { get; }
    DbSet<JobApplication> Applications { get; }
    DbSet<ApplicationStatusHistory> ApplicationStatusHistory { get; }
    DbSet<CalendarEvent> CalendarEvents { get; }
    DbSet<Reminder> Reminders { get; }
    DbSet<Resume> Resumes { get; }
    DbSet<ResumeAnalysis> ResumeAnalyses { get; }

    /// <summary>Saves changes. Unique-constraint violations surface as <see cref="Common.ConflictException"/>.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
