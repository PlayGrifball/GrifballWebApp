using GrifballWebApp.Application.Chat;
using GrifballWebApp.Application.Database;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace GrifballWebApp.Application.Test;

/// <summary>What the chat keeps of a user (<see cref="ChatAccounts"/>): a reply's spend, added however many replies save at once.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class ChatAccountsUnitTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 15, 12, 0, 0, TimeSpan.Zero));
    private readonly FakeApplicationDbContext _db = new();
    private ServiceProvider _services = null!;

    [TearDown]
    public async Task TearDown()
    {
        if (_services is not null)
            await _services.DisposeAsync();
        await _db.DisposeAsync();
    }

    /// <summary>Accounts whose first scope's context runs <paramref name="first"/> before it saves; the rest, plain ones.</summary>
    private ChatAccounts Accounts(Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor first)
    {
        var made = 0;
        _services = new ServiceCollection()
            .AddScoped<IApplicationDbContext>(_ => Interlocked.Increment(ref made) == 1 ? _db.NewContextLike(first) : _db.NewContextLike())
            .BuildServiceProvider();
        return new ChatAccounts(_services.GetRequiredService<IServiceScopeFactory>(), _time, NullLogger<ChatAccounts>.Instance);
    }

    /// <summary>Another reply's cost saved to <paramref name="userId"/>'s month, on a context of its own.</summary>
    private async Task OtherReplySaves(int userId, long costMicros)
    {
        await using var other = _db.NewContextLike();
        var month = ChatMonth.MonthOf(_time.GetUtcNow().UtcDateTime);
        var row = await other.ChatMonths.SingleOrDefaultAsync(m => m.UserID == userId && m.Month == month);
        if (row is null)
        {
            row = ChatMonth.Begin(userId, month);
            other.ChatMonths.Add(row);
        }
        row.Record(costMicros, 1, 1);
        await other.SaveChangesAsync();
    }

    [Test]
    public async Task RecordAsync_AnotherReplySavedMeanwhile_AddsToWhatItSaved()
    {
        await OtherReplySaves(7, 100);
        var accounts = Accounts(new BeforeFirstSave(_ => OtherReplySaves(7, 20)));

        await accounts.RecordAsync(7, 3, 10, 10);

        var month = await _db.ChatMonths.AsNoTracking().SingleAsync();
        Assert.That((month.CostMicros, month.Replies), Is.EqualTo((123L, 3)), "Neither reply's cost lost");
    }

    [Test]
    public async Task RecordAsync_AnotherReplyMadeTheMonthMeanwhile_AddsToIt()
    {
        var accounts = Accounts(new BeforeFirstSave(_ => OtherReplySaves(7, 20)));

        await accounts.RecordAsync(7, 3, 10, 10);

        var month = await _db.ChatMonths.AsNoTracking().SingleAsync();
        Assert.That((month.CostMicros, month.Replies), Is.EqualTo((23L, 2)));
    }

    [Test]
    public async Task MonthAsync_ThisMonthsOnly()
    {
        var accounts = Accounts(new BeforeFirstSave(_ => Task.CompletedTask));
        await accounts.RecordAsync(7, 1_500_000, 0, 0);

        var november = await accounts.MonthAsync(7);
        _time.Advance(TimeSpan.FromDays(20));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(november?.Spent, Is.EqualTo(1.5m));
            Assert.That(await accounts.MonthAsync(7), Is.Null, "December: November's spend is past");
            Assert.That(await accounts.MonthAsync(8), Is.Null);
        }
    }
}
