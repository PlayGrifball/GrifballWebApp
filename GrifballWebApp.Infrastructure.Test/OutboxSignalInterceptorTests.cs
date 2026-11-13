using GrifballWebApp.Domain.Shared;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// OutboxSignalInterceptor, on a real (temp-file SQLite) GrifballContext behind a tiny DI container (the same shape
/// AddGrifballDatabase wires it in production, so AddInterceptors sees the generic options builder it needs): no
/// database server, host or network. Each test gets its own file and its own OutboxSignal.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class OutboxSignalInterceptorTests : SqliteContextFixture
{
    private static readonly TimeSpan ShortWait = TimeSpan.FromSeconds(2);

    private readonly OutboxSignal _signal = new();
    private ServiceProvider? _provider;
    private AsyncServiceScope? _scope;

    /// <summary>The context from a scope of a DI container that adds the interceptor, as AddGrifballDatabase does.</summary>
    protected override GrifballContext CreateContext()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new OutboxSignalInterceptor(_signal));
        services.AddDbContext<GrifballContext>((provider, options) => options
            .UseGrifballDatabase(DatabaseProvider.Sqlite, ConnectionString)
            .AddInterceptors(provider.GetRequiredService<OutboxSignalInterceptor>()));
        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateAsyncScope();
        return _scope.Value.ServiceProvider.GetRequiredService<GrifballContext>();
    }

    /// <summary>The scope that made the context, then the container.</summary>
    protected override async ValueTask DisposeContextOwnersAsync()
    {
        if (_scope is { } scope)
            await scope.DisposeAsync();
        if (_provider is not null)
            await _provider.DisposeAsync();
    }

    private static OutboxMessage NewMessage() => new()
    {
        Kind = "test.kind",
        Payload = "{}",
    };

    [Test]
    public async Task SaveChangesAsync_OutsideATransaction_SignalsAtOnce()
    {
        Context.Outbox.Add(NewMessage());

        await Context.SaveChangesAsync();

        Assert.That(await _signal.WaitAsync(ShortWait, CancellationToken.None), Is.True);
    }

    [Test]
    public async Task SaveChanges_Sync_OutsideATransaction_SignalsAtOnce()
    {
        Context.Outbox.Add(NewMessage());

        // The sync SavingChanges/SavedChanges overrides are the case under test, not a sync-over-async shortcut.
#pragma warning disable VSTHRD103 // Call async methods when in an async method
        Context.SaveChanges();
#pragma warning restore VSTHRD103

        Assert.That(await _signal.WaitAsync(ShortWait, CancellationToken.None), Is.True);
    }

    [Test]
    public async Task SaveChangesAsync_NoOutboxMessageAdded_DoesNotSignal()
    {
        Context.Outbox.Add(NewMessage());
        await Context.SaveChangesAsync();
        // Drain the signal from the first save, so this test observes only the second.
        await _signal.WaitAsync(ShortWait, CancellationToken.None);

        var message = await Context.Outbox.SingleAsync();
        message.LastError = "no outbox row added by this save";
        await Context.SaveChangesAsync();

        Assert.That(await _signal.WaitAsync(TimeSpan.FromMilliseconds(100), CancellationToken.None), Is.False);
    }

    [Test]
    public async Task SaveChangesAsync_InsideATransaction_NotYetCommitted_DoesNotSignal()
    {
        await using var transaction = await Context.Database.BeginTransactionAsync();
        Context.Outbox.Add(NewMessage());
        await Context.SaveChangesAsync();

        Assert.That(await _signal.WaitAsync(TimeSpan.FromMilliseconds(100), CancellationToken.None), Is.False);
    }

    [Test]
    public async Task SaveChangesAsync_InsideATransaction_SignalsOnceCommitted()
    {
        await using var transaction = await Context.Database.BeginTransactionAsync();
        Context.Outbox.Add(NewMessage());
        await Context.SaveChangesAsync();

        await transaction.CommitAsync();

        Assert.That(await _signal.WaitAsync(ShortWait, CancellationToken.None), Is.True);
    }

    [Test]
    public async Task SaveChangesAsync_InsideATransaction_RolledBack_NeverSignals()
    {
        await using var transaction = await Context.Database.BeginTransactionAsync();
        Context.Outbox.Add(NewMessage());
        await Context.SaveChangesAsync();

        await transaction.RollbackAsync();

        Assert.That(await _signal.WaitAsync(TimeSpan.FromMilliseconds(100), CancellationToken.None), Is.False);
    }
}
