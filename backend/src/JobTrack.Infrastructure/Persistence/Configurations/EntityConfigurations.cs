using System.Text.Json;
using JobTrack.Domain.Entities;
using JobTrack.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobTrack.Infrastructure.Persistence.Configurations;

// Enums are stored as integers with explicit, stable values (see Domain/Enums) so range comparisons such as
// "status <= Offer" stay valid in SQL.
//
// Delete rules:
//   User → everything                  CASCADE  (account deletion removes all personal data)
//   Company → JobOffer                 NO ACTION (blocked in the service with a 409)
//   JobOffer → Application             NO ACTION (blocked in the service with a 409)
//   Application → StatusHistory        CASCADE
//   Application/Company → Event        SET NULL (the event survives, unlinked)
//   Application/Event → Reminder       SET NULL
//   Resume → Application.ResumeId      SET NULL
//   Resume → ResumeAnalysis            CASCADE  (analyses quote the CV, so they go with it)
//   JobOffer → ResumeAnalysis          SET NULL (title/company snapshots keep it readable)

internal sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.Property(u => u.FullName).HasMaxLength(AppUser.MaxFullNameLength).IsRequired();
        builder.Property(u => u.TimeZone).HasMaxLength(64).IsRequired();
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.TokenHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => t.UserId);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("Companies");
        builder.ConfigureOwned();
        builder.Property(c => c.Name).HasMaxLength(Company.MaxNameLength).IsRequired();
        builder.Property(c => c.NormalizedName).HasMaxLength(Company.MaxNameLength).IsRequired();
        builder.Property(c => c.Website).HasMaxLength(Company.MaxUrlLength);
        builder.Property(c => c.Industry).HasMaxLength(Company.MaxShortTextLength);
        builder.Property(c => c.Location).HasMaxLength(Company.MaxShortTextLength);
        builder.Property(c => c.Description).HasMaxLength(Company.MaxDescriptionLength);
        builder.Property(c => c.ContactName).HasMaxLength(Company.MaxShortTextLength);
        builder.Property(c => c.ContactEmail).HasMaxLength(Company.MaxShortTextLength);
        builder.Property(c => c.ContactPhone).HasMaxLength(50);
        builder.Property(c => c.Notes).HasMaxLength(Company.MaxNotesLength);
        builder.HasIndex(c => new { c.UserId, c.NormalizedName })
            .IsUnique()
            .HasDatabaseName("IX_Companies_UserId_NormalizedName");
    }
}

internal sealed class JobOfferConfiguration : IEntityTypeConfiguration<JobOffer>
{
    public void Configure(EntityTypeBuilder<JobOffer> builder)
    {
        builder.ToTable("JobOffers", t =>
        {
            t.HasCheckConstraint("CK_JobOffers_SalaryRange",
                "\"SalaryMin\" IS NULL OR \"SalaryMax\" IS NULL OR \"SalaryMin\" <= \"SalaryMax\"");
        });
        builder.ConfigureOwned();
        builder.Property(o => o.Title).HasMaxLength(JobOffer.MaxTitleLength).IsRequired();
        builder.Property(o => o.Description).HasMaxLength(JobOffer.MaxDescriptionLength);
        builder.Property(o => o.Url).HasMaxLength(JobOffer.MaxUrlLength);
        builder.Property(o => o.Location).HasMaxLength(JobOffer.MaxLocationLength);
        builder.Property(o => o.SalaryMin).HasPrecision(12, 2);
        builder.Property(o => o.SalaryMax).HasPrecision(12, 2);
        builder.Property(o => o.Currency).HasMaxLength(3).IsFixedLength();
        builder.Property(o => o.Notes).HasMaxLength(JobOffer.MaxNotesLength);
        builder.Property(o => o.RequiredSkills).HasColumnType("text[]");
        builder.Property(o => o.PreferredSkills).HasColumnType("text[]");
        builder.HasOne(o => o.Company)
            .WithMany(c => c.JobOffers)
            .HasForeignKey(o => o.CompanyId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(o => new { o.UserId, o.SavedAt });
        builder.HasIndex(o => o.CompanyId);
    }
}

internal sealed class JobApplicationConfiguration : IEntityTypeConfiguration<JobApplication>
{
    public void Configure(EntityTypeBuilder<JobApplication> builder)
    {
        builder.ToTable("Applications");
        builder.ConfigureOwned();
        builder.Property(a => a.CoverLetter).HasMaxLength(JobApplication.MaxCoverLetterLength);
        builder.Property(a => a.Notes).HasMaxLength(JobApplication.MaxNotesLength);
        builder.Property(a => a.NextAction).HasMaxLength(JobApplication.MaxNextActionLength);
        builder.Property(a => a.RejectionReason).HasMaxLength(JobApplication.MaxRejectionReasonLength);
        builder.HasOne(a => a.JobOffer)
            .WithOne(o => o.Application)
            .HasForeignKey<JobApplication>(a => a.JobOfferId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(a => a.Resume)
            .WithMany()
            .HasForeignKey(a => a.ResumeId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasMany(a => a.StatusHistory)
            .WithOne(h => h.Application)
            .HasForeignKey(h => h.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(a => a.StatusHistory).UsePropertyAccessMode(PropertyAccessMode.Field);

        // One application per offer (offers are per user, so this is also one per user and offer).
        builder.HasIndex(a => a.JobOfferId).IsUnique().HasDatabaseName("IX_Applications_JobOfferId");
        builder.HasIndex(a => new { a.UserId, a.Status });
        builder.HasIndex(a => new { a.UserId, a.FollowUpOn });
    }
}

internal sealed class ApplicationStatusHistoryConfiguration : IEntityTypeConfiguration<ApplicationStatusHistory>
{
    public void Configure(EntityTypeBuilder<ApplicationStatusHistory> builder)
    {
        builder.ToTable("ApplicationStatusHistory");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).ValueGeneratedNever();
        builder.Property(h => h.Note).HasMaxLength(ApplicationStatusHistory.MaxNoteLength);
        builder.HasIndex(h => new { h.ApplicationId, h.ChangedAt });
        builder.HasIndex(h => h.ChangedAt);
    }
}

internal sealed class CalendarEventConfiguration : IEntityTypeConfiguration<CalendarEvent>
{
    public void Configure(EntityTypeBuilder<CalendarEvent> builder)
    {
        builder.ToTable("CalendarEvents", t =>
            t.HasCheckConstraint("CK_CalendarEvents_Range", "\"EndsAt\" > \"StartsAt\""));
        builder.ConfigureOwned();
        builder.Property(e => e.Title).HasMaxLength(CalendarEvent.MaxTitleLength).IsRequired();
        builder.Property(e => e.TimeZone).HasMaxLength(CalendarEvent.MaxTimeZoneLength).IsRequired();
        builder.Property(e => e.Location).HasMaxLength(CalendarEvent.MaxLocationLength);
        builder.Property(e => e.MeetingUrl).HasMaxLength(CalendarEvent.MaxUrlLength);
        builder.Property(e => e.Notes).HasMaxLength(CalendarEvent.MaxNotesLength);
        builder.HasOne(e => e.Company).WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.Application)
            .WithMany()
            .HasForeignKey(e => e.ApplicationId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(e => new { e.UserId, e.StartsAt });
        builder.HasIndex(e => e.ApplicationId);
        builder.HasIndex(e => e.CompanyId);
    }
}

internal sealed class ReminderConfiguration : IEntityTypeConfiguration<Reminder>
{
    public void Configure(EntityTypeBuilder<Reminder> builder)
    {
        builder.ToTable("Reminders");
        builder.ConfigureOwned();
        builder.Property(r => r.Title).HasMaxLength(Reminder.MaxTitleLength).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(Reminder.MaxDescriptionLength);
        builder.HasOne(r => r.Application)
            .WithMany()
            .HasForeignKey(r => r.ApplicationId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(r => r.CalendarEvent)
            .WithMany()
            .HasForeignKey(r => r.CalendarEventId)
            .OnDelete(DeleteBehavior.SetNull);
        // Supports "pending reminders by due date", the query a future notification dispatcher would run.
        builder.HasIndex(r => new { r.UserId, r.IsCompleted, r.DueAt });
        builder.HasIndex(r => r.ApplicationId);
        builder.HasIndex(r => r.CalendarEventId);
    }
}

internal sealed class ResumeConfiguration : IEntityTypeConfiguration<Resume>
{
    public void Configure(EntityTypeBuilder<Resume> builder)
    {
        builder.ToTable("Resumes");
        builder.ConfigureOwned();
        builder.Property(r => r.FileName).HasMaxLength(Resume.MaxFileNameLength).IsRequired();
        builder.Property(r => r.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(r => r.StorageKey).HasMaxLength(100).IsRequired();
        builder.Property(r => r.Sha256).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(r => r.ExtractedText).IsRequired();
        builder.HasMany(r => r.Analyses)
            .WithOne(a => a.Resume)
            .HasForeignKey(a => a.ResumeId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(r => new { r.UserId, r.CreatedAt });
    }
}

internal sealed class ResumeAnalysisConfiguration : IEntityTypeConfiguration<ResumeAnalysis>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<ResumeAnalysis> builder)
    {
        builder.ToTable("ResumeAnalyses", t =>
            t.HasCheckConstraint("CK_ResumeAnalyses_MatchScore", "\"MatchScore\" BETWEEN 0 AND 100"));
        builder.ConfigureOwned();
        builder.Property(a => a.JobTitleSnapshot).HasMaxLength(JobOffer.MaxTitleLength).IsRequired();
        builder.Property(a => a.CompanyNameSnapshot).HasMaxLength(Company.MaxNameLength).IsRequired();
        builder.Property(a => a.Provider).HasMaxLength(50).IsRequired();
        builder.Property(a => a.Model).HasMaxLength(100).IsRequired();
        builder.Property(a => a.PromptVersion).HasMaxLength(20).IsRequired();
        builder.Property(a => a.Result)
            .HasColumnType("jsonb")
            .HasConversion(
                result => JsonSerializer.Serialize(result, JsonOptions),
                json => JsonSerializer.Deserialize<ResumeAnalysisResult>(json, JsonOptions) ?? ResumeAnalysisResult.Empty);
        builder.HasOne(a => a.JobOffer)
            .WithMany()
            .HasForeignKey(a => a.JobOfferId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(a => new { a.UserId, a.CreatedAt });
        builder.HasIndex(a => a.ResumeId);
        builder.HasIndex(a => a.JobOfferId);
    }
}
