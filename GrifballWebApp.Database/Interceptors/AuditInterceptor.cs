using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GrifballWebApp.Database.Interceptors;

public class AuditInterceptor : SaveChangesInterceptor
{
    private readonly IServiceProvider _serviceProvider;

    public AuditInterceptor(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        SetAuditFields(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        SetAuditFields(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void SetAuditFields(DbContext? context)
    {
        if (context == null)
            return;

        // For now, we'll set the user ID to a default value or get from service provider
        // This can be enhanced to get the current user from HttpContext or other sources
        var currentUserId = GetCurrentUserId();

        var auditableEntries = context.ChangeTracker.Entries<IAuditable>();

        foreach (var entry in auditableEntries)
        {
            var now = DateTime.UtcNow;

            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedByID = currentUserId;
                    entry.Entity.CreatedAt = now;
                    entry.Entity.ModifiedByID = currentUserId;
                    entry.Entity.ModifiedAt = now;
                    break;

                case EntityState.Modified:
                    // Don't update CreatedBy fields on modification
                    entry.Entity.ModifiedByID = currentUserId;
                    entry.Entity.ModifiedAt = now;
                    break;
            }
        }
    }

    private int? GetCurrentUserId()
    {
        // This is a placeholder implementation.
        // In a real application, you would get the current user ID from:
        // - HttpContext.User claims
        // - Current user service
        // - Authentication context
        
        // For now, return null to indicate system/unknown user
        // This can be enhanced based on the application's authentication setup
        return null;
    }
}