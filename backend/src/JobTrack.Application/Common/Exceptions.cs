namespace JobTrack.Application.Common;

/// <summary>The resource does not exist or does not belong to the caller (both map to 404 to avoid leaking existence).</summary>
public sealed class NotFoundException(string resource)
    : Exception($"{resource} no encontrado.")
{
    public string Resource { get; } = resource;
}

/// <summary>The request conflicts with the current state, e.g. a duplicate or a delete blocked by related data.</summary>
public sealed class ConflictException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>Invalid credentials or session. Always reported with the same generic message to avoid user enumeration.</summary>
public sealed class AuthenticationFailedException(string message = "Credenciales no válidas.") : Exception(message);

/// <summary>An external dependency (e.g. the AI provider) failed or is not available. Maps to 502/503/429.</summary>
public sealed class ExternalServiceException(string code, string message, int statusCode) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}
