using GrifballWebApp.Application.DomainEvents;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using GrifballWebApp.Application.Outbox;
using GrifballWebApp.Infrastructure.Outbox;
using GrifballWebApp.Infrastructure.Services;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// DomainEventsInterceptor on a real (temp-file SQLite) GrifballContext, wired as AddGrifballDatabase wires it: a save's
/// domain events go to the dispatcher once committed, and only then.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DomainEventsInterceptorTests : SqliteContextFixture
{
    private static readonly string[] OwnTransactionPost = ["\"own-transaction\""];
    private static readonly string[] TwoPosts = ["\"first-of-two\"", "\"second-of-two\""];
    private static readonly string[] CanceledThenSaved = ["\"canceled\""];

    private readonly RecordingDispatcher _dispatcher = new();
    private ServiceProvider? _provider;
    private AsyncServiceScope? _scope;

    private sealed class RecordingDispatcher : IDomainEventDispatcher
    {
        public List<IDomainEvent[]> Dispatched { get; } = [];

        public Task Dispatch(IReadOnlyList<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
        {
            Dispatched.Add([.. domainEvents]);
            return Task.CompletedTask;
        }
    }

    /// <summary>A before-commit handler: queues an outbox message for each user changed.</summary>
    private sealed class QueueAPost(IOutbox outbox) : IBeforeCommitHandler<UserChanged>
    {
        public Task Handle(UserChanged domainEvent, IReadOnlyList<IDomainEvent> commit, CancellationToken cancellationToken)
            => outbox.EnqueueAsync("test.post", domainEvent.User.UserName, null, cancellationToken);
    }

    /// <summary>A before-commit handler that fails: the change must not be made.</summary>
    private sealed class FailToPost : IBeforeCommitHandler<XboxUserChanged>
    {
        public Task Handle(XboxUserChanged domainEvent, IReadOnlyList<IDomainEvent> commit, CancellationToken cancellationToken)
            => throw new InvalidOperationException("the post could not be queued");
    }

    /// <summary>When armed, a write is canceled as it runs, as a request that ends mid-save cancels it.</summary>
    private sealed class CancelTheWrite : DbCommandInterceptor
    {
        public bool Armed { get; set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
            => Armed ? throw new OperationCanceledException("the request ended") : base.ReaderExecutingAsync(command, eventData, result, cancellationToken);

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
            => Armed ? throw new OperationCanceledException("the request ended") : base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);

        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
            => Armed ? throw new OperationCanceledException("the request ended") : base.ReaderExecuting(command, eventData, result);

        public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
            => Armed ? throw new OperationCanceledException("the request ended") : base.NonQueryExecuting(command, eventData, result);
    }

    private readonly CancelTheWrite _cancelTheWrite = new();

    protected override GrifballContext CreateContext()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventDispatcher>(_dispatcher);
        services.AddSingleton<IBeforeCommitDispatcher>(provider => new BeforeCommitDispatcher(provider.GetRequiredService<IServiceProviderIsService>()));
        services.AddScoped<IOutbox>(provider => new ContextOutbox(provider.GetRequiredService<GrifballContext>()));
        services.AddScoped<IBeforeCommitHandler<UserChanged>, QueueAPost>();
        services.AddScoped<IBeforeCommitHandler<XboxUserChanged>, FailToPost>();
        services.AddScoped<DomainEventsInterceptor>();
        services.AddDbContext<GrifballContext>((provider, options) => options
            .UseGrifballDatabase(DatabaseProvider.Sqlite, ConnectionString)
            .AddInterceptors(provider.GetRequiredService<DomainEventsInterceptor>(), _cancelTheWrite));
        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateAsyncScope();
        return _scope.Value.ServiceProvider.GetRequiredService<GrifballContext>();
    }

    protected override async ValueTask DisposeContextOwnersAsync()
    {
        if (_scope is { } scope)
            await scope.DisposeAsync();
        if (_provider is not null)
            await _provider.DisposeAsync();
    }

    private static IReadOnlyList<IDomainEvent> Pending(User user) => ((IHasDomainEvents)user).DomainEvents;

    [Test]
    public async Task ASave_DispatchesItsEvents_Once_AndTheAggregateForgetsThem()
    {
        var user = User.Register("registered", displayName: "Shown");
        Context.Users.Add(user);

        await Context.SaveChangesAsync();
        await Context.SaveChangesAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_dispatcher.Dispatched, Has.Count.EqualTo(1));
            Assert.That(_dispatcher.Dispatched[0], Is.EqualTo(new IDomainEvent[] { new UserChanged(user) }));
            Assert.That(Pending(user), Is.Empty);
        }
    }

    [Test]
    public async Task InATransaction_DispatchesOnlyOnceItCommits_EverySaveOfIt()
    {
        var first = User.Register("first");
        var second = User.Register("second");

        await using (var transaction = await Context.Database.BeginTransactionAsync())
        {
            Context.Users.Add(first);
            await Context.SaveChangesAsync();
            Context.Users.Add(second);
            await Context.SaveChangesAsync();
            Assert.That(_dispatcher.Dispatched, Is.Empty, "not before the commit");
            await transaction.CommitAsync();
        }

        Assert.That(_dispatcher.Dispatched.Single(), Is.EqualTo(new IDomainEvent[] { new UserChanged(first), new UserChanged(second) }));
    }

    [Test]
    public async Task ARolledBackTransaction_DispatchesNothing()
    {
        await using (var transaction = await Context.Database.BeginTransactionAsync())
        {
            Context.Users.Add(User.Register("rolled-back"));
            await Context.SaveChangesAsync();
            await transaction.RollbackAsync();
        }

        Assert.That(_dispatcher.Dispatched, Is.Empty);
    }

    [Test]
    public async Task ASaveWithoutEvents_DispatchesNothing()
    {
        var user = User.Register("quiet");
        ((IHasDomainEvents)user).ClearDomainEvents();
        Context.Users.Add(user);

        await Context.SaveChangesAsync();

        Assert.That(_dispatcher.Dispatched, Is.Empty);
    }

    /// <summary>The test posts committed, as another connection reads them.</summary>
    private async Task<string[]> Posts()
    {
        await using var fresh = new GrifballContext(new DbContextOptionsBuilder<GrifballContext>().UseGrifballDatabase(DatabaseProvider.Sqlite, ConnectionString).Options);
        return await fresh.Outbox.Where(m => m.Kind == "test.post").Select(m => m.Payload).ToArrayAsync();
    }

    [Test]
    public async Task ASaveOutsideATransaction_ItsBeforeCommitHandlersPost_InATransactionOfItsOwn_ThenItsEventsGoOut()
    {
        Context.Users.Add(User.Register("own-transaction"));

        await Context.SaveChangesAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Posts(), Is.EqualTo(OwnTransactionPost));
            Assert.That(Context.Database.CurrentTransaction, Is.Null, "committed");
            Assert.That(_dispatcher.Dispatched, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public async Task InATransaction_TheBeforeCommitHandlersRun_AsItCommits_ForEverySave_AndPostWithIt()
    {
        await using (var transaction = await Context.Database.BeginTransactionAsync())
        {
            Context.Users.Add(User.Register("first-of-two"));
            await Context.SaveChangesAsync();
            Context.Users.Add(User.Register("second-of-two"));
            await Context.SaveChangesAsync();
            Assert.That(await Context.Outbox.CountAsync(m => m.Kind == "test.post"), Is.Zero, "not before the commit");
            await transaction.CommitAsync();
        }

        Assert.That(await Posts(), Is.EquivalentTo(TwoPosts));
    }

    [Test]
    public async Task ARolledBackTransaction_PostsNothing()
    {
        await using (var transaction = await Context.Database.BeginTransactionAsync())
        {
            Context.Users.Add(User.Register("never-posted"));
            await Context.SaveChangesAsync();
            await transaction.RollbackAsync();
        }

        Assert.That(await Posts(), Is.Empty);
    }

    [Test]
    public void ASynchronousSave_DispatchesItsEvents_WithoutWaitingForThem()
    {
        var account = DiscordUser.FromDiscord(1, "synchronous");
        Context.DiscordUsers.Add(account);

        Context.SaveChanges();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_dispatcher.Dispatched.Single(), Is.EqualTo(new IDomainEvent[] { new DiscordUserChanged(account) }));
            Assert.That(((IHasDomainEvents)account).DomainEvents, Is.Empty);
        }
    }

    [Test]
    public void ASynchronousSave_WhoseEventsHaveABeforeCommitHandler_IsRefused()
    {
        Context.Users.Add(User.Register("synchronous"));

        Assert.That(() => Context.SaveChanges(), Throws.InstanceOf<InvalidOperationException>());
    }

    [Test]
    public async Task AFailedSave_DispatchesAndPostsNothing_AndTheAggregateKeepsItsEvents()
    {
        Context.Users.Add(User.Seed(userName: "one", normalizedUserName: "SAME"));
        await Context.SaveChangesAsync();
        var twin = User.Seed(userName: "two", normalizedUserName: "SAME");
        twin.Rename("Twin");
        Context.Users.Add(twin);

        Assert.That(async () => await Context.SaveChangesAsync(), Throws.InstanceOf<DbUpdateException>());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_dispatcher.Dispatched, Is.Empty);
            Assert.That(Pending(twin), Is.Not.Empty, "kept for the next try");
            Assert.That(await Posts(), Is.Empty);
            Assert.That(Context.Database.CurrentTransaction, Is.Null, "its own transaction rolled back");
        }
    }

    [Test]
    public async Task AContextWithoutAScope_RefusesASaveThatNeedsBeforeCommitHandlers()
    {
        var interceptor = new DomainEventsInterceptor(_dispatcher, _provider!.GetRequiredService<IBeforeCommitDispatcher>(), services: null);
        await using var unscoped = new GrifballContext(new DbContextOptionsBuilder<GrifballContext>()
            .UseGrifballDatabase(DatabaseProvider.Sqlite, ConnectionString).AddInterceptors(interceptor).Options);
        unscoped.Users.Add(User.Register("unscoped"));

        Assert.That(async () => await unscoped.SaveChangesAsync(), Throws.InstanceOf<InvalidOperationException>());
    }

    [Test]
    public async Task ABeforeCommitHandlerFailing_OnASaveOutsideATransaction_RollsItsTransactionBack_AndDispatchesNothing()
    {
        Context.XboxUsers.Add(XboxUser.FromXbox(1, "never-saved"));

        Assert.That(async () => await Context.SaveChangesAsync(), Throws.InstanceOf<InvalidOperationException>());

        await using var fresh = new GrifballContext(new DbContextOptionsBuilder<GrifballContext>().UseGrifballDatabase(DatabaseProvider.Sqlite, ConnectionString).Options);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Context.Database.CurrentTransaction, Is.Null, "rolled back");
            Assert.That(_dispatcher.Dispatched, Is.Empty);
            Assert.That(await fresh.XboxUsers.CountAsync(), Is.Zero);
        }
    }

    [Test]
    public void ASynchronousTransaction_DispatchesOnceItCommits()
    {
        var account = DiscordUser.FromDiscord(2, "synchronous-transaction");

        using (var transaction = Context.Database.BeginTransaction())
        {
            Context.DiscordUsers.Add(account);
            Context.SaveChanges();
            Assert.That(_dispatcher.Dispatched, Is.Empty, "not before the commit");
            transaction.Commit();
        }

        Assert.That(_dispatcher.Dispatched.Single(), Is.EqualTo(new IDomainEvent[] { new DiscordUserChanged(account) }));
    }

    [Test]
    public void ASynchronousRollback_DispatchesNothing()
    {
        using (var transaction = Context.Database.BeginTransaction())
        {
            Context.DiscordUsers.Add(DiscordUser.FromDiscord(3, "rolled-back"));
            Context.SaveChanges();
            transaction.Rollback();
        }

        Assert.That(_dispatcher.Dispatched, Is.Empty);
    }

    [Test]
    public async Task ASynchronousCommit_OfEventsWithBeforeCommitHandlers_IsRefused()
    {
        await using var transaction = await Context.Database.BeginTransactionAsync();
        Context.Users.Add(User.Register("committed-synchronously"));
        await Context.SaveChangesAsync();

        Assert.That(() => transaction.Commit(), Throws.InstanceOf<InvalidOperationException>());
    }

    [Test]
    public void AFailedSynchronousSave_DispatchesNothing_AndTheAggregateKeepsItsEvents()
    {
        Context.DiscordUsers.Add(DiscordUser.FromDiscord(4, "first"));
        Context.SaveChanges();
        _dispatcher.Dispatched.Clear();
        Context.ChangeTracker.Clear();
        var twin = DiscordUser.FromDiscord(4, "twin");
        Context.DiscordUsers.Add(twin);

        Assert.That(() => Context.SaveChanges(), Throws.InstanceOf<DbUpdateException>());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_dispatcher.Dispatched, Is.Empty);
            Assert.That(((IHasDomainEvents)twin).DomainEvents, Is.Not.Empty, "kept for the next try");
        }
    }

    [Test]
    public void AFailedSynchronousSave_InATransaction_KeepsItsEarlierSavesEvents_ForTheCommit()
    {
        var first = DiscordUser.FromDiscord(5, "first");

        using (var transaction = Context.Database.BeginTransaction())
        {
            Context.DiscordUsers.Add(first);
            Context.SaveChanges();
            Context.Entry(first).State = EntityState.Detached;
            var twin = DiscordUser.FromDiscord(5, "twin");
            Context.DiscordUsers.Add(twin);
            Assert.That(() => Context.SaveChanges(), Throws.InstanceOf<DbUpdateException>());
            Context.Entry(twin).State = EntityState.Detached;
            transaction.Commit();
        }

        Assert.That(_dispatcher.Dispatched.Single(), Is.EqualTo(new IDomainEvent[] { new DiscordUserChanged(first) }));
    }

    [Test]
    public async Task ATransactionDisposedWithoutARollback_ItsEventsNeverGoOut_WithALaterSave()
    {
        var stale = DiscordUser.FromDiscord(6, "disposed");
        await using (await Context.Database.BeginTransactionAsync())
        {
            Context.DiscordUsers.Add(stale);
            await Context.SaveChangesAsync();
        }
        Context.ChangeTracker.Clear();
        var later = DiscordUser.FromDiscord(7, "later");
        Context.DiscordUsers.Add(later);

        await Context.SaveChangesAsync();

        Assert.That(_dispatcher.Dispatched.Single(), Is.EqualTo(new IDomainEvent[] { new DiscordUserChanged(later) }));
    }

    [Test]
    public async Task ATransactionDisposedWithoutARollback_ItsEventsNeitherPostNorGoOut_WithTheNextTransaction()
    {
        await using (await Context.Database.BeginTransactionAsync())
        {
            Context.Users.Add(User.Register("never-committed"));
            await Context.SaveChangesAsync();
        }
        Context.ChangeTracker.Clear();
        var account = DiscordUser.FromDiscord(8, "committed");

        await using (var transaction = await Context.Database.BeginTransactionAsync())
        {
            Context.DiscordUsers.Add(account);
            await Context.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Posts(), Is.Empty);
            Assert.That(_dispatcher.Dispatched.Single(), Is.EqualTo(new IDomainEvent[] { new DiscordUserChanged(account) }));
        }
    }

    [Test]
    public async Task ASaveCanceledInItsOwnTransaction_RollsItBack_AndCanBeTriedAgain()
    {
        Context.Users.Add(User.Register("canceled"));
        _cancelTheWrite.Armed = true;

        Assert.That(async () => await Context.SaveChangesAsync(), Throws.InstanceOf<OperationCanceledException>());
        var openAfterTheCancel = Context.Database.CurrentTransaction is not null;
        _cancelTheWrite.Armed = false;
        await Context.SaveChangesAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(openAfterTheCancel, Is.False, "its own transaction rolled back");
            Assert.That(await Posts(), Is.EqualTo(CanceledThenSaved), "posted once, by the save that was made");
            Assert.That(_dispatcher.Dispatched, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public void ASynchronousSaveCanceled_DispatchesNothing_AndTheAggregateKeepsItsEvents()
    {
        var account = DiscordUser.FromDiscord(9, "canceled");
        Context.DiscordUsers.Add(account);
        _cancelTheWrite.Armed = true;

        Assert.That(() => Context.SaveChanges(), Throws.InstanceOf<OperationCanceledException>());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_dispatcher.Dispatched, Is.Empty);
            Assert.That(((IHasDomainEvents)account).DomainEvents, Is.Not.Empty, "kept for the next try");
        }
    }
}
