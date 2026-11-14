using GrifballWebApp.Application.Identity;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// UserSessionsInterceptor on a real (temp-file SQLite) GrifballContext, wired as AddGrifballDatabase wires it: which saves end
/// whose live sessions, and when.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class UserSessionsInterceptorTests : SqliteContextFixture
{
    private readonly RecordingSessions _sessions = new();
    private ServiceProvider? _provider;
    private AsyncServiceScope? _scope;

    private sealed class RecordingSessions : IUserSessions
    {
        public List<int[]> Ended { get; } = [];

        public Task EndAsync(IReadOnlyCollection<int> userIds, CancellationToken ct = default)
        {
            Ended.Add([.. userIds]);
            return Task.CompletedTask;
        }
    }

    protected override GrifballContext CreateContext()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IUserSessions>(_sessions);
        services.AddScoped<UserSessionsInterceptor>();
        services.AddDbContext<GrifballContext>((provider, options) => options
            .UseGrifballDatabase(DatabaseProvider.Sqlite, ConnectionString)
            .AddInterceptors(provider.GetRequiredService<UserSessionsInterceptor>()));
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

    private async Task<User> AddUser(string name)
    {
        var user = User.Register(name);
        Context.Users.Add(user);
        await Context.SaveChangesAsync();
        _sessions.Ended.Clear();
        return user;
    }

    private Task<Role> ARole() => Context.Roles.OrderBy(r => r.Id).FirstAsync();

    [Test]
    public async Task ARoleAddedOrRemoved_EndsThatUsersSessions()
    {
        var user = await AddUser("staffer");
        var role = await ARole();

        Context.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        await Context.SaveChangesAsync();
        var added = _sessions.Ended.ToList();
        _sessions.Ended.Clear();
        Context.UserRoles.Remove(await Context.UserRoles.SingleAsync(r => r.UserId == user.Id));
        await Context.SaveChangesAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(added, Is.EqualTo(new[] { new[] { user.Id } }));
            Assert.That(_sessions.Ended, Is.EqualTo(new[] { new[] { user.Id } }));
        }
    }

    [Test]
    public async Task ANewSecurityStamp_ALockout_OrADeletedUser_EndsTheirSessions()
    {
        var signedOut = await AddUser("signed-out");
        var locked = await AddUser("locked");
        var deleted = await AddUser("deleted");

        signedOut.SecurityStamp = Guid.NewGuid().ToString();
        locked.LockoutEnd = DateTimeOffset.UtcNow.AddDays(1);
        Context.Users.Remove(deleted);
        await Context.SaveChangesAsync();

        Assert.That(_sessions.Ended.Single(), Is.EquivalentTo(new[] { signedOut.Id, locked.Id, deleted.Id }));
    }

    [Test]
    public async Task OtherChangesToAUser_OrANewUser_EndNoSessions()
    {
        var user = await AddUser("renamed");

        user.Rename("New name");
        Context.Users.Add(User.Register("newcomer"));
        await Context.SaveChangesAsync();

        Assert.That(_sessions.Ended, Is.Empty);
    }

    [Test]
    public async Task InATransaction_EndsSessionsOnlyOnceItCommits()
    {
        var user = await AddUser("staffer");
        var role = await ARole();

        await using (var transaction = await Context.Database.BeginTransactionAsync())
        {
            Context.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
            await Context.SaveChangesAsync();
            Assert.That(_sessions.Ended, Is.Empty, "not committed yet");
            await transaction.CommitAsync();
        }

        Assert.That(_sessions.Ended, Is.EqualTo(new[] { new[] { user.Id } }));
    }

    [Test]
    public async Task InATransactionRolledBack_EndsNoSessions()
    {
        var user = await AddUser("staffer");
        var role = await ARole();

        await using (var transaction = await Context.Database.BeginTransactionAsync())
        {
            Context.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
            await Context.SaveChangesAsync();
            await transaction.RollbackAsync();
        }
        await Context.SaveChangesAsync();

        Assert.That(_sessions.Ended, Is.Empty);
    }

    [Test]
    public async Task SyncSaves_EndSessionsToo()
    {
        var user = await AddUser("staffer");

        user.SecurityStamp = Guid.NewGuid().ToString();
#pragma warning disable VSTHRD103 // The sync overrides are the case under test
        Context.SaveChanges();
        using (var transaction = Context.Database.BeginTransaction())
        {
            user.LockoutEnd = DateTimeOffset.UtcNow.AddDays(1);
            Context.SaveChanges();
            transaction.Commit();
        }
#pragma warning restore VSTHRD103

        Assert.That(_sessions.Ended, Is.EqualTo(new[] { new[] { user.Id }, new[] { user.Id } }));
    }

    [Test]
    public async Task AFailedSave_EndsNoSessions()
    {
        var user = await AddUser("staffer");
        user.SecurityStamp = Guid.NewGuid().ToString();
        // A role that doesn't exist: the save fails on its foreign key
        Context.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = 999_999 });

        Assert.That(async () => await Context.SaveChangesAsync(), Throws.InstanceOf<DbUpdateException>());
        Context.ChangeTracker.Clear();
        Context.Users.Add(User.Register("after"));
        await Context.SaveChangesAsync();

        Assert.That(_sessions.Ended, Is.Empty);
    }
}
