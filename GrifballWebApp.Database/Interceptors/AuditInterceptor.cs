using GrifballWebApp.Database.Models;
using GrifballWebApp.Database.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

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
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var currentUserService = scope.ServiceProvider.GetService<ICurrentUserService>();
            return currentUserService?.GetCurrentUserId();
        }
        catch
        {
            // If we can't get the current user service (e.g., during migrations, tests, etc.)
            // just return null to indicate system/unknown user
            return null;
        }
    }
}