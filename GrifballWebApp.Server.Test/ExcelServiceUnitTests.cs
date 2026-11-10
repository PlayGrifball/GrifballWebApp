using GrifballWebApp.Server.Excel;
using Google.Apis.Sheets.v4;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Quartz;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Jobs;
using GrifballWebApp.Infrastructure.Excel;
using GrifballWebApp.Infrastructure.Jobs;

namespace GrifballWebApp.Server.Test;

/// <summary>ExcelServiceTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class ExcelServiceUnitTests
{
    [TestCase(nameof(ExcelController.DefaultSheetInfo))]
    [TestCase(nameof(ExcelController.ExportAll))]
    [TestCase(nameof(ExcelController.ExportAllStatus))]
    [TestCase(nameof(ExcelController.AppendMatch))]
    public void Controller_Actions_RequireCommissionerOrSysadmin(string action)
    {
        var attr = ControllerTestHelpers.ActionAuthorize(typeof(ExcelController), action);
        Assert.That(attr, Is.Not.Null);
        Assert.That(attr!.Roles, Is.EqualTo("Commissioner,Sysadmin"));
    }
}
