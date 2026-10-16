using FluentValidation;
using JobTrack.Application.Applications;
using JobTrack.Application.Calendar;
using JobTrack.Application.Companies;
using JobTrack.Application.Dashboard;
using JobTrack.Application.JobOffers;
using JobTrack.Application.Reminders;
using JobTrack.Application.Resumes;
using Microsoft.Extensions.DependencyInjection;

namespace JobTrack.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        services.AddScoped<CompanyService>();
        services.AddScoped<JobOfferService>();
        services.AddScoped<ApplicationService>();
        services.AddScoped<CalendarEventService>();
        services.AddScoped<ReminderService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<ResumeService>();
        services.AddScoped<ResumeAnalysesService>();
        services.AddSingleton<IResumeAnalysisService, LocalKeywordAnalyzer>();
        return services;
    }
}
