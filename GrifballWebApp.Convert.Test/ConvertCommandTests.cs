using GrifballWebApp.Convert;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.Conversion;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Logging.Abstractions;

using System.Runtime.InteropServices;

namespace GrifballWebApp.Convert.Test;

/// <summary>DatabaseConversionTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class ConvertCommandTests
{
    [Test]
    public async Task Command_TakesItsSettingsFromTheCommandLine()
    {
        string[] args = ["--Source:Provider=Oracle", "--Source:ConnectionString=x", "--Target:Provider=Postgres", "--Target:ConnectionString=x"];

        Assert.That(await ConvertCommand.MainAsync(args), Is.EqualTo(2));
    }

    [TestCase(null, "x", "Postgres", "x", TestName = "Command_NeedsTheSourceProvider")]
    [TestCase("Oracle", "x", "Postgres", "x", TestName = "Command_NeedsAKnownProvider")]
    [TestCase("SqlServer", null, "Postgres", "x", TestName = "Command_NeedsTheSourceConnectionString")]
    [TestCase("SqlServer", "x", null, "x", TestName = "Command_NeedsTheTargetProvider")]
    [TestCase("SqlServer", "x", "Postgres", null, TestName = "Command_NeedsTheTargetConnectionString")]
    public async Task Command_Settings(string? sourceProvider, string? sourceConnection, string? targetProvider, string? targetConnection)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Source:Provider"] = sourceProvider,
            ["Source:ConnectionString"] = sourceConnection,
            ["Target:Provider"] = targetProvider,
            ["Target:ConnectionString"] = targetConnection,
        }).Build();

        Assert.That(await ConvertCommand.RunAsync(configuration, NullLoggerFactory.Instance), Is.EqualTo(2));
    }

    private static IConfiguration ValidSourceAndTarget() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Source:Provider"] = "SqlServer",
        ["Source:ConnectionString"] = "source",
        ["Target:Provider"] = "Postgres",
        ["Target:ConnectionString"] = "target",
    }).Build();

    [Test]
    public async Task Command_WhenTheConversionSucceeds_ReturnsZero()
    {
        using var cancellation = new CancellationTokenSource();
        (DatabaseEndpoint Source, DatabaseEndpoint Target, CancellationToken Token)? call = null;

        var code = await ConvertCommand.RunAsync(ValidSourceAndTarget(), NullLoggerFactory.Instance,
            (source, target, ct) =>
            {
                call = (source, target, ct);
                return Task.CompletedTask;
            },
            cancellation.Token);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(code, Is.Zero);
            Assert.That(call, Is.Not.Null, "the conversion ran");
            Assert.That((call?.Source.Provider, call?.Source.ConnectionString), Is.EqualTo((DatabaseProvider.SqlServer, "source")));
            Assert.That((call?.Target.Provider, call?.Target.ConnectionString), Is.EqualTo((DatabaseProvider.Postgres, "target")));
            Assert.That(call?.Token, Is.EqualTo(cancellation.Token), "the caller's token is passed on");
        }
    }

    // A refusal is the operator's to fix, not a crash: its message alone, no stack trace
    [Test]
    public async Task Command_WhenTheConversionRefuses_LogsTheReasonAndReturnsOne()
    {
        using var logs = new FakeLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(logging => logging.AddProvider(logs));

        var code = await ConvertCommand.RunAsync(ValidSourceAndTarget(), loggerFactory,
            (_, _, _) => Task.FromException(new ConversionException("The target database already has tables.")),
            CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(code, Is.EqualTo(1));
            var error = logs.Collector.GetSnapshot().Single(e => e.Level == LogLevel.Error);
            Assert.That(error.Message, Is.EqualTo("The target database already has tables."));
            Assert.That(error.Exception, Is.Null);
        }
    }

    [Test]
    public async Task Command_WhenTheConversionThrows_LogsTheExceptionAndReturnsOne()
    {
        using var logs = new FakeLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(logging => logging.AddProvider(logs));
        var failure = new InvalidOperationException("boom");

        var code = await ConvertCommand.RunAsync(ValidSourceAndTarget(), loggerFactory,
            (_, _, _) => Task.FromException(failure),
            CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(code, Is.EqualTo(1));
            var error = logs.Collector.GetSnapshot().Single(e => e.Level == LogLevel.Error);
            Assert.That(error.Message, Is.EqualTo("The conversion failed"));
            Assert.That(error.Exception, Is.SameAs(failure));
        }
    }

    // Ctrl+C (MainAsync cancels the token) stops the conversion: a warning and 130, not a failure with a stack trace
    [Test]
    public async Task Command_WhenStopped_LogsAWarningAndReturns130()
    {
        using var logs = new FakeLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(logging => logging.AddProvider(logs));
        using var cancellation = new CancellationTokenSource();

        var code = await ConvertCommand.RunAsync(ValidSourceAndTarget(), loggerFactory,
            async (_, _, ct) =>
            {
                await cancellation.CancelAsync();
                ct.ThrowIfCancellationRequested();
            },
            cancellation.Token);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(code, Is.EqualTo(130));
            Assert.That(logs.Collector.GetSnapshot().Select(e => (e.Level, e.Message)), Does.Contain((LogLevel.Warning, "The conversion was stopped")));
            Assert.That(logs.Collector.GetSnapshot().Where(e => e.Level == LogLevel.Error), Is.Empty);
        }
    }

    // SqlClient reports a cancelled command as a SqlException: once the caller has stopped the conversion, whatever it
    // throws is the stop
    [Test]
    public async Task Command_WhenStopped_ReturnsAs130WhateverTheStopThrew()
    {
        using var logs = new FakeLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(logging => logging.AddProvider(logs));
        using var cancellation = new CancellationTokenSource();

        var code = await ConvertCommand.RunAsync(ValidSourceAndTarget(), loggerFactory,
            async (_, _, _) =>
            {
                await cancellation.CancelAsync();
                throw new InvalidOperationException("Operation cancelled by user.");
            },
            cancellation.Token);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(code, Is.EqualTo(130));
            Assert.That(logs.Collector.GetSnapshot().Where(e => e.Level == LogLevel.Error), Is.Empty);
        }
    }

    [Test]
    public void OnStopSignal_TheFirstStopsTheConversion_ASecondEndsTheProcess()
    {
        using var cancellation = new CancellationTokenSource();
        var first = new PosixSignalContext(PosixSignal.SIGTERM);
        var second = new PosixSignalContext(PosixSignal.SIGINT);

        ConvertCommand.OnStopSignal(first, cancellation);
        ConvertCommand.OnStopSignal(second, cancellation);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(cancellation.IsCancellationRequested, Is.True);
            Assert.That(first.Cancel, Is.True, "the first is handled: the process carries on to clean up");
            Assert.That(second.Cancel, Is.False, "the second takes its default: the process ends");
        }
    }

    // A cancellation the caller didn't ask for (a timeout inside the conversion) is a failure like any other
    [Test]
    public async Task Command_WhenTheConversionCancelsItself_ReturnsOne()
    {
        var code = await ConvertCommand.RunAsync(ValidSourceAndTarget(), NullLoggerFactory.Instance,
            (_, _, _) => Task.FromException(new TaskCanceledException("timed out")),
            CancellationToken.None);

        Assert.That(code, Is.EqualTo(1));
    }
}
