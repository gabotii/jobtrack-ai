namespace JobTrack.Application.Abstractions;

/// <summary>Identity of the authenticated caller, always taken from the validated access token, never from the request body.</summary>
public interface ICurrentUser
{
    /// <summary>The authenticated user's id, or <see cref="Guid.Empty"/> when the request is anonymous.</summary>
    Guid UserId { get; }

    /// <summary>The user's id; throws when the request is anonymous.</summary>
    Guid RequiredUserId => UserId != Guid.Empty
        ? UserId
        : throw new UnauthorizedAccessException("The request is not authenticated.");
}
