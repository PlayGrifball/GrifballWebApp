using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using GrifballWebApp.Application.Dtos;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Profile;

namespace GrifballWebApp.Server.Test;

/// <summary>UserMergeAuditTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class UserMergeAuditUnitTests
{
    [Test]
    public void MergeUndoRefused_IsAConflictForTheUser()
    {
        // GlobalExceptionHandler is internal: its Expected through reflection
        var type = typeof(GrifballWebApp.Server.Program).Assembly.GetType("GrifballWebApp.Server.GlobalExceptionHandler", throwOnError: true)!;
        var expected = type.GetMethod("Expected", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!;
        var problem = (ProblemDetails)expected.Invoke(null, [new MergeUndoRefusedException("Can't undo: x")])!;
        Assert.That((problem.Status, problem.Detail), Is.EqualTo(((int?)409, "Can't undo: x")));
    }
}
