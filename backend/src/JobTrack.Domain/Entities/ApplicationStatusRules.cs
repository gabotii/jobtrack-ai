using JobTrack.Domain.Enums;

namespace JobTrack.Domain.Entities;

/// <summary>
/// Allowed transitions between application statuses.
/// <list type="bullet">
/// <item>Active pipeline: Saved → ToApply → Applied → Screening → Interview → Offer. Forward moves may skip stages
/// (companies often skip steps). Backward moves are only allowed between Saved and ToApply, because once an
/// application has been sent it cannot become "not sent" again.</item>
/// <item>Accepted is only reachable from Offer and is final.</item>
/// <item>Rejected is reachable from any post-application stage (Applied … Offer).</item>
/// <item>Withdrawn is reachable from any non-final stage.</item>
/// <item>Rejected and Withdrawn can be reopened into any active stage, to correct mistakes or resumed processes.</item>
/// </list>
/// </summary>
public static class ApplicationStatusRules
{
    private static readonly ApplicationStatus[] AllStatuses = Enum.GetValues<ApplicationStatus>();

    /// <summary>Statuses a new application may start in.</summary>
    public static bool IsValidInitialStatus(ApplicationStatus status) => IsActive(status);

    /// <summary>Statuses that belong to the active pipeline (not closed).</summary>
    public static bool IsActive(ApplicationStatus status) => status <= ApplicationStatus.Offer;

    /// <summary>Statuses where the candidate has already sent the application and the process is ongoing.</summary>
    public static bool IsInProgress(ApplicationStatus status) =>
        status is >= ApplicationStatus.Applied and <= ApplicationStatus.Offer;

    public static bool IsClosed(ApplicationStatus status) => !IsActive(status);

    public static bool CanTransition(ApplicationStatus from, ApplicationStatus to)
    {
        if (from == to || from == ApplicationStatus.Accepted)
        {
            return false;
        }

        return to switch
        {
            ApplicationStatus.Accepted => from == ApplicationStatus.Offer,
            ApplicationStatus.Rejected => IsInProgress(from),
            ApplicationStatus.Withdrawn => IsActive(from),
            // Reopening a closed application into any active stage.
            _ when from is ApplicationStatus.Rejected or ApplicationStatus.Withdrawn => true,
            // Backward moves only inside the pre-application stages.
            ApplicationStatus.Saved or ApplicationStatus.ToApply => from <= ApplicationStatus.ToApply,
            // Forward moves within the active pipeline.
            _ => to > from,
        };
    }

    public static IReadOnlyList<ApplicationStatus> AllowedTransitionsFrom(ApplicationStatus from) =>
        AllStatuses.Where(to => CanTransition(from, to)).ToArray();

    /// <summary>Spanish display label, used in user-facing error messages.</summary>
    public static string Label(ApplicationStatus status) => status switch
    {
        ApplicationStatus.Saved => "Guardada",
        ApplicationStatus.ToApply => "Por postular",
        ApplicationStatus.Applied => "Postulada",
        ApplicationStatus.Screening => "Evaluación inicial",
        ApplicationStatus.Interview => "Entrevista",
        ApplicationStatus.Offer => "Oferta recibida",
        ApplicationStatus.Accepted => "Aceptada",
        ApplicationStatus.Rejected => "Rechazada",
        ApplicationStatus.Withdrawn => "Retirada",
        _ => status.ToString(),
    };
}
