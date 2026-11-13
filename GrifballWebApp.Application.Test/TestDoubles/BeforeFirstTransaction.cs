using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GrifballWebApp.Application.Test.TestDoubles;

/// <summary>
/// Runs an action once, as the context's first transaction is about to begin (before it does, so what the action writes
/// is committed and the transaction sees it): a test's way to stage another request's write landing between a service's
/// own checks and its transaction (a race). The action writes through a context of its own on the same connection
/// (<see cref="FakeApplicationDbContext.NewContextLike"/>). Both ways a service opens one go through it:
/// Database.BeginTransactionAsync, and StartTransactionAsync, which calls it.
/// </summary>
internal sealed class BeforeFirstTransaction(Func<CancellationToken, Task> action) : DbTransactionInterceptor
{
    private bool _ran;

    public override async ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(DbConnection connection,
        TransactionStartingEventData eventData, InterceptionResult<DbTransaction> result, CancellationToken cancellationToken = default)
    {
        if (!_ran)
        {
            _ran = true;
            await action(cancellationToken);
        }
        return result;
    }
}
