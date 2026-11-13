using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GrifballWebApp.Application.Test.TestDoubles;

/// <summary>
/// Runs an action once, as the context's first save begins: a test's way to stage another request's write landing
/// between a service's own check and its save (a race). The action writes through a context of its own on the same
/// connection (<see cref="FakeApplicationDbContext"/>'s options over Context.Database.GetDbConnection()).
/// </summary>
internal sealed class BeforeFirstSave(Func<CancellationToken, Task> action) : SaveChangesInterceptor
{
    private bool _ran;

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (!_ran)
        {
            _ran = true;
            await action(cancellationToken);
        }
        return result;
    }
}
