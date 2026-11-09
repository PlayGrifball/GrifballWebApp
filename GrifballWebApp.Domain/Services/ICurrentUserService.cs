using System.Security.Claims;

namespace GrifballWebApp.Domain.Services;

/// <summary>The app user a change is made by, for the audit columns (AuditInterceptor). Implemented by the database
/// layer's CurrentUserService.</summary>
public interface ICurrentUserService
{
    void SetCurrentUserIdFromClaims(ClaimsPrincipal? user);
    void SetCurrentUserId(int userId);
    int? GetCurrentUserId();
}
