using GrifballWebApp.Application.DomainEvents;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using GrifballWebApp.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
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

    protected override GrifballContext CreateContext()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventDispatcher>(_dispatcher);
        services.AddScoped<DomainEventsInterceptor>();
        services.AddDbContext<GrifballContext>((provider, options) => options
            .UseGrifballDatabase(DatabaseProvider.Sqlite, ConnectionString)
            .AddInterceptors(provider.GetRequiredService<DomainEventsInterceptor>()));
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
}
