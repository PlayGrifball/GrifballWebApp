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
public class OutboxSignalInterceptorTests
{
    private static readonly TimeSpan ShortWait = TimeSpan.FromSeconds(2);

    private string _path = null!;
    private ServiceProvider _provider = null!;
    private AsyncServiceScope _scope;
    private GrifballContext _context = null!;
    private OutboxSignal _signal = null!;

    [SetUp]
    public async Task SetUp()
    {
        _path = Path.Combine(Path.GetTempPath(), $"grif-outbox-signal-{Guid.NewGuid():N}.db");
        _signal = new OutboxSignal();
        var services = new ServiceCollection();
        services.AddSingleton(new OutboxSignalInterceptor(_signal));
        services.AddDbContext<GrifballContext>((provider, options) => options
            .UseGrifballDatabase(DatabaseProvider.Sqlite, $"Data Source={_path};Pooling=False")
            .AddInterceptors(provider.GetRequiredService<OutboxSignalInterceptor>()));
        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateAsyncScope();
        _context = _scope.ServiceProvider.GetRequiredService<GrifballContext>();
        try
        {
            await _context.Database.EnsureCreatedAsync();
        }
        catch
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
            if (File.Exists(_path))
                File.Delete(_path);
            throw;
        }
    }

    [TearDown]
    public async Task TearDown()
    {
        await _scope.DisposeAsync();
        await _provider.DisposeAsync();
        if (File.Exists(_path))
            File.Delete(_path);
    }

    private static OutboxMessage NewMessage() => new()
    {
        Kind = "test.kind",
        Payload = "{}",
    };

    [Test]
    public async Task SaveChangesAsync_OutsideATransaction_SignalsAtOnce()
    {
        _context.Outbox.Add(NewMessage());

        await _context.SaveChangesAsync();

        Assert.That(await _signal.WaitAsync(ShortWait, CancellationToken.None), Is.True);
    }

    [Test]
    public async Task SaveChanges_Sync_OutsideATransaction_SignalsAtOnce()
    {
        _context.Outbox.Add(NewMessage());

        // The sync SavingChanges/SavedChanges overrides are the case under test, not a sync-over-async shortcut.
#pragma warning disable VSTHRD103 // Call async methods when in an async method
        _context.SaveChanges();
#pragma warning restore VSTHRD103

        Assert.That(await _signal.WaitAsync(ShortWait, CancellationToken.None), Is.True);
    }

    [Test]
    public async Task SaveChangesAsync_NoOutboxMessageAdded_DoesNotSignal()
    {
        _context.Outbox.Add(NewMessage());
        await _context.SaveChangesAsync();
        // Drain the signal from the first save, so this test observes only the second.
        await _signal.WaitAsync(ShortWait, CancellationToken.None);

        var message = await _context.Outbox.SingleAsync();
        message.LastError = "no outbox row added by this save";
        await _context.SaveChangesAsync();

        Assert.That(await _signal.WaitAsync(TimeSpan.FromMilliseconds(100), CancellationToken.None), Is.False);
    }

    [Test]
    public async Task SaveChangesAsync_InsideATransaction_NotYetCommitted_DoesNotSignal()
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        _context.Outbox.Add(NewMessage());
        await _context.SaveChangesAsync();

        Assert.That(await _signal.WaitAsync(TimeSpan.FromMilliseconds(100), CancellationToken.None), Is.False);
    }

    [Test]
    public async Task SaveChangesAsync_InsideATransaction_SignalsOnceCommitted()
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        _context.Outbox.Add(NewMessage());
        await _context.SaveChangesAsync();

        await transaction.CommitAsync();

        Assert.That(await _signal.WaitAsync(ShortWait, CancellationToken.None), Is.True);
    }

    [Test]
    public async Task SaveChangesAsync_InsideATransaction_RolledBack_NeverSignals()
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        _context.Outbox.Add(NewMessage());
        await _context.SaveChangesAsync();

        await transaction.RollbackAsync();

        Assert.That(await _signal.WaitAsync(TimeSpan.FromMilliseconds(100), CancellationToken.None), Is.False);
    }
}
