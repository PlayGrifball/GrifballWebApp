using GrifballWebApp.Database.Models;
using GrifballWebApp.Database.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GrifballWebApp.Database.Interceptors;

public class AuditInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUserService;

    public AuditInterceptor(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
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

        var now = DateTime.UtcNow;
        var currentUserId = GetCurrentUserId();

        var auditableEntries = context.ChangeTracker.Entries<IAuditable>();

        foreach (var entry in auditableEntries)
        {
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
        return _currentUserService.GetCurrentUserId();
    }
}