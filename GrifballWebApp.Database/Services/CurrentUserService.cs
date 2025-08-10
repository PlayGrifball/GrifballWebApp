using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace GrifballWebApp.Database.Services;

public interface ICurrentUserService
{
    void SetCurrentUserIdFromClaims(ClaimsPrincipal? user);
    void SetCurrentUserId(int userId);
    int? GetCurrentUserId();
}

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private int? _currentUserId;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void SetCurrentUserIdFromClaims(ClaimsPrincipal? user)
    {
        if (user is null)
            return;

        if (user?.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out var userId))
            {
                _currentUserId = userId;
            }
        }
    }

    public void SetCurrentUserId(int userId)
    {
        _currentUserId = userId;
    }

    public int? GetCurrentUserId()
    {
        // If this is SignalR, Discord, or somebackend system process, we may not have a user context.
        // In that case, we return the cached user ID if it exists. (Hopefully they set it!)
        if (_currentUserId.HasValue)
        {
            return _currentUserId;
        }
        SetCurrentUserIdFromClaims(_httpContextAccessor.HttpContext?.User);
        return _currentUserId;
    }
}