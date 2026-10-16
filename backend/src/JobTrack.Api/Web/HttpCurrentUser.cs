using JobTrack.Application.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;

namespace JobTrack.Api.Web;

/// <summary>Reads the user id from the validated JWT "sub" claim of the current request.</summary>
internal sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid UserId
    {
        get
        {
            var principal = accessor.HttpContext?.User;
            if (principal?.Identity?.IsAuthenticated != true)
            {
                return Guid.Empty;
            }

            return Guid.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) ? id : Guid.Empty;
        }
    }
}
