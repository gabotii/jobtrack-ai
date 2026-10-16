using JobTrack.Application.Common;
using JobTrack.Domain.Entities;
using JobTrack.Domain.Enums;
using JobTrack.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace JobTrack.Infrastructure.Persistence;

/// <summary>
/// Creates one demo account with fictitious companies, offers, applications, events and reminders. It is idempotent
/// (does nothing if the demo account exists), never touches other accounts, and is only invoked explicitly through
/// the <c>seed-demo</c> command, which refuses to run in Production.
/// </summary>
public sealed class DemoDataSeeder(AppDbContext db, UserManager<AppUser> userManager, TimeProvider timeProvider)
{
    public const string DemoTimeZone = "Europe/Madrid";

    /// <returns>false when the demo account already existed.</returns>
    public async Task<bool> SeedAsync(string email, string password, CancellationToken ct = default)
    {
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return false;
        }

        var user = new AppUser
        {
            Id = Guid.CreateVersion7(),
            UserName = email,
            Email = email,
            FullName = "Usuario Demo",
            TimeZone = DemoTimeZone,
        };
        var created = await userManager.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            throw new InvalidOperationException(
                "Could not create the demo user: " + string.Join("; ", created.Errors.Select(e => e.Description)));
        }

        var zone = TimeZoneInfo.FindSystemTimeZoneById(DemoTimeZone);
        var now = timeProvider.GetUtcNow();
        var today = TimeZones.LocalDate(now, zone);
        DateTimeOffset At(int daysFromToday, int hour, int minute = 0) =>
            TimeZones.StartOfDayUtc(today.AddDays(daysFromToday), zone).AddHours(hour).AddMinutes(minute);

        var companies = new[]
        {
            Company(user.Id, "Nébula Software", "Software", "Madrid, España", "https://nebula.example.com",
                "Producto SaaS B2B de gestión de proyectos.", "Laura Méndez", "talento@nebula.example.com"),
            Company(user.Id, "Andes Fintech", "Servicios financieros", "Santiago, Chile", "https://andesfintech.example.com",
                "Pagos digitales para pymes de Latinoamérica.", "Martín Rojas", null),
            Company(user.Id, "Verde Logística", "Logística", "Valencia, España", null,
                "Optimización de rutas de última milla.", null, null),
            Company(user.Id, "Pixel Forge Studio", "Videojuegos", "Barcelona, España", "https://pixelforge.example.com",
                "Estudio independiente de videojuegos móviles.", "Clara Puig", "jobs@pixelforge.example.com"),
            Company(user.Id, "DataHarbor Analytics", "Datos e IA", "Remoto", "https://dataharbor.example.com",
                "Consultora de ingeniería de datos.", null, null),
        };
        db.Companies.AddRange(companies);

        var offers = new[]
        {
            Offer(user.Id, companies[0], "Desarrollador/a Full-Stack .NET + React", WorkMode.Hybrid, "Madrid",
                ["C#", ".NET", "React", "TypeScript", "PostgreSQL"], ["Docker", "Azure"], 38_000, 46_000, now.AddDays(-60)),
            Offer(user.Id, companies[1], "Backend Engineer (C#)", WorkMode.Remote, "Remoto (LatAm)",
                ["C#", "ASP.NET Core", "SQL", "Microservicios"], ["Kafka"], 3_000, 4_200, now.AddDays(-45), "USD"),
            Offer(user.Id, companies[2], "Frontend Developer React", WorkMode.OnSite, "Valencia",
                ["React", "TypeScript", "CSS"], ["Testing Library", "Vite"], 30_000, 36_000, now.AddDays(-40)),
            Offer(user.Id, companies[3], "Tools Programmer", WorkMode.Hybrid, "Barcelona",
                ["C#", "Unity", "Python"], ["CI/CD"], null, null, now.AddDays(-30)),
            Offer(user.Id, companies[4], "Data Engineer", WorkMode.Remote, "Remoto",
                ["Python", "SQL", "Airflow"], ["dbt", "AWS"], 42_000, 52_000, now.AddDays(-20)),
            Offer(user.Id, companies[0], "Tech Lead Frontend", WorkMode.Hybrid, "Madrid",
                ["React", "Arquitectura frontend", "Liderazgo"], ["Next.js"], 55_000, 65_000, now.AddDays(-10)),
            Offer(user.Id, companies[1], "Ingeniero/a de Plataforma", WorkMode.Remote, "Remoto",
                ["Kubernetes", "Terraform", "Go"], [], null, null, now.AddDays(-5)),
        };
        db.JobOffers.AddRange(offers);

        var interview = Application(user.Id, offers[0], ApplicationStatus.Applied, At(-50, 10), today.AddDays(-50),
            (ApplicationStatus.Screening, At(-40, 12)), (ApplicationStatus.Interview, At(-30, 9)));
        interview.NextAction = "Preparar la entrevista técnica";
        interview.FollowUpOn = today.AddDays(2);

        var screening = Application(user.Id, offers[1], ApplicationStatus.Applied, At(-35, 18), today.AddDays(-35),
            (ApplicationStatus.Screening, At(-20, 16)));
        screening.NextAction = "Escribir al reclutador para conocer el estado";
        screening.FollowUpOn = today.AddDays(-1);

        var rejected = Application(user.Id, offers[2], ApplicationStatus.Applied, At(-38, 11), today.AddDays(-38),
            (ApplicationStatus.Rejected, At(-25, 9)));

        var offer = Application(user.Id, offers[3], ApplicationStatus.Applied, At(-28, 9), today.AddDays(-28),
            (ApplicationStatus.Interview, At(-18, 10)), (ApplicationStatus.Offer, At(-3, 15)));
        offer.NextAction = "Responder a la oferta antes del viernes";
        offer.FollowUpOn = today.AddDays(3);

        var applied = Application(user.Id, offers[4], ApplicationStatus.ToApply, At(-15, 20), today.AddDays(-15),
            (ApplicationStatus.Applied, At(-12, 9)));
        applied.FollowUpOn = today.AddDays(5);

        var toApply = Application(user.Id, offers[5], ApplicationStatus.ToApply, At(-8, 19), today.AddDays(-8));
        toApply.NextAction = "Adaptar el CV a liderazgo técnico";

        db.Applications.AddRange(interview, screening, rejected, offer, applied, toApply);

        var technicalInterview = Event(user.Id, "Entrevista técnica", CalendarEventType.Interview,
            At(2, 10), At(2, 11, 30), interview, companies[0], meetingUrl: "https://meet.example.com/nebula-tech");
        var hrCall = Event(user.Id, "Llamada con RR. HH.", CalendarEventType.Call,
            At(5, 16), At(5, 16, 30), screening, companies[1]);
        var pastCall = Event(user.Id, "Primera llamada", CalendarEventType.Call,
            At(-21, 12), At(-21, 12, 30), offer, companies[3]);
        pastCall.SetStatus(CalendarEventStatus.Completed);
        var followUp = Event(user.Id, "Seguimiento de la oferta", CalendarEventType.FollowUp,
            At(3, 9), At(3, 9, 15), offer, companies[3]);
        db.CalendarEvents.AddRange(technicalInterview, hrCall, pastCall, followUp);

        var completedReminder = new Reminder(user.Id, "Enviar portafolio actualizado", At(-10, 18), ReminderPriority.Medium)
        {
            ApplicationId = offer.Id,
        };
        completedReminder.Complete(At(-10, 17));
        db.Reminders.AddRange(
            new Reminder(user.Id, "Repasar preguntas de system design", At(1, 19), ReminderPriority.High)
            {
                Description = "Caché, colas y consistencia eventual.",
                ApplicationId = interview.Id,
                CalendarEventId = technicalInterview.Id,
            },
            new Reminder(user.Id, "Contactar a Martín Rojas", At(-1, 10), ReminderPriority.Medium)
            {
                ApplicationId = screening.Id,
            },
            new Reminder(user.Id, "Revisar condiciones de la oferta", At(2, 12), ReminderPriority.High)
            {
                ApplicationId = offer.Id,
            },
            completedReminder);

        await db.SaveChangesAsync(ct);
        return true;
    }

    private static Company Company(Guid userId, string name, string industry, string location, string? website,
        string description, string? contactName, string? contactEmail) =>
        new(userId, name)
        {
            Industry = industry,
            Location = location,
            Website = website,
            Description = description,
            ContactName = contactName,
            ContactEmail = contactEmail,
        };

    private static JobOffer Offer(Guid userId, Company company, string title, WorkMode mode, string location,
        List<string> required, List<string> preferred, decimal? min, decimal? max, DateTimeOffset savedAt,
        string currency = "EUR")
    {
        var offer = new JobOffer(userId, company.Id, title, savedAt)
        {
            Location = location,
            WorkMode = mode,
            ContractType = ContractType.FullTime,
            RequiredSkills = required,
            PreferredSkills = preferred,
            Description = $"Buscamos una persona para el puesto de {title} en {company.Name}. " +
                          $"Requisitos: {string.Join(", ", required)}. Valorable: {string.Join(", ", preferred)}.",
        };
        offer.SetSalary(min, max, min is null && max is null ? null : currency);
        return offer;
    }

    private static JobApplication Application(Guid userId, JobOffer offer, ApplicationStatus initial,
        DateTimeOffset createdAt, DateOnly createdOn, params (ApplicationStatus Status, DateTimeOffset At)[] moves)
    {
        var application = JobApplication.Create(userId, offer.Id, initial, createdAt, createdOn);
        application.Source = ApplicationSource.LinkedIn;
        foreach (var (status, at) in moves)
        {
            application.ChangeStatus(status, at, DateOnly.FromDateTime(at.UtcDateTime), note: null,
                rejectionReason: status == ApplicationStatus.Rejected ? "Buscaban más experiencia en el sector." : null);
        }

        return application;
    }

    private static CalendarEvent Event(Guid userId, string title, CalendarEventType type, DateTimeOffset start,
        DateTimeOffset end, JobApplication application, Company company, string? meetingUrl = null)
    {
        var calendarEvent = new CalendarEvent(userId, title, type) { MeetingUrl = meetingUrl };
        calendarEvent.Schedule(start, end, DemoTimeZone);
        calendarEvent.LinkTo(application.Id, company.Id);
        return calendarEvent;
    }
}
