using GrifballWebApp.Historical.Import;
using GrifballWebApp.Historical.Sources;
using GrifballWebApp.Historical.Worker;
using GrifballWebApp.Infrastructure.Jobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Quartz;
using Quartz.Extensibility;

namespace GrifballWebApp.Historical.Test;

/// <summary>AddHistoricalImport and AddHistoricalJobs: the import's services, settings, download client and job.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class HistoricalServiceCollectionExtensionsTests
{
    private static IConfiguration Config(Dictionary<string, string?> settings) => new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

    private static ServiceCollection AddHistoricalImport(Dictionary<string, string?>? settings = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHistoricalImport(Config(settings ?? []));
        return services;
    }

    [TestCase(typeof(IHistoricalImportService), typeof(HistoricalImportService), ServiceLifetime.Transient)]
    [TestCase(typeof(HistoricalImportJobs), typeof(HistoricalImportJobs), ServiceLifetime.Transient)]
    [TestCase(typeof(HistoricalImportSignal), typeof(HistoricalImportSignal), ServiceLifetime.Singleton)]
    [TestCase(typeof(HistoricalSourceService), typeof(HistoricalSourceService), ServiceLifetime.Transient)]
    [TestCase(typeof(HistoricalImportRunner), typeof(HistoricalImportRunner), ServiceLifetime.Transient)]
    [TestCase(typeof(HistoricalImportSchedule), typeof(HistoricalImportSchedule), ServiceLifetime.Transient)]
    public void AddHistoricalImport_RegistersTheService(Type service, Type implementation, ServiceLifetime lifetime)
    {
        var registration = AddHistoricalImport().Single(d => d.ServiceType == service);

        Assert.That((registration.ImplementationType, registration.Lifetime), Is.EqualTo((implementation, lifetime)));
    }

    [Test]
    public void AddHistoricalImport_RunsTheWorker()
    {
        Assert.That(AddHistoricalImport().Any(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(HistoricalImportWorker)), Is.True);
    }

    [Test]
    public void AddHistoricalImport_BindsEachSettingsClassToItsOwnSection()
    {
        // One key under each class's section, a value none of the defaults has: a class bound to another section (or a
        // misspelled one) keeps its default and fails here
        using var provider = AddHistoricalImport(new()
        {
            ["HistoricalImport:MatchPullAttempts"] = "7",
            ["HistoricalImport:Worker:ReportLinesKept"] = "42",
            ["HistoricalImport:Guardrail:MaxRemovedFraction"] = "0.75",
        }).BuildServiceProvider();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(provider.GetRequiredService<IOptions<HistoricalImportOptions>>().Value.MatchPullAttempts, Is.EqualTo(7));
            Assert.That(provider.GetRequiredService<IOptions<HistoricalImportWorkerOptions>>().Value.ReportLinesKept, Is.EqualTo(42));
            Assert.That(provider.GetRequiredService<IOptions<HistoricalGuardrailOptions>>().Value.MaxRemovedFraction, Is.EqualTo(0.75m));
        }
    }

    [Test]
    public void AddHistoricalImport_TheSourcesHttpClient_SaysWhoItIs_AndWaitsItsOwnTime()
    {
        using var provider = AddHistoricalImport().BuildServiceProvider();

        using var sources = provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(HistoricalSourceFetcher));

        using (Assert.EnterMultipleScope())
        {
            Assert.That((sources.Timeout, sources.MaxResponseContentBufferSize), Is.EqualTo((TimeSpan.FromMinutes(2), 64L * 1024 * 1024)));
            Assert.That(sources.DefaultRequestHeaders.UserAgent.ToString(), Is.EqualTo("GrifballWebApp/1.0 (historical archive)"));
        }
    }

    [Test]
    public void AddHistoricalJobs_RegistersTheRefreshJob()
    {
        var jobs = new RecurringJobs();

        HistoricalServiceCollectionExtensions.AddHistoricalJobs(jobs);

        Assert.That(jobs.All.Single(j => j.Type == typeof(HistoricalRefreshJob)).Job.Key, Is.EqualTo(HistoricalRefreshJob.Definition.Key));
    }

    /// <summary>A rolling update's older pod stores the refresh job under the name its type had there.</summary>
    [TestCase("GrifballWebApp.Server.Historical.HistoricalRefreshJob, GrifballWebApp.Server")]
    [TestCase("GrifballWebApp.Infrastructure.Historical.HistoricalRefreshJob, GrifballWebApp.Infrastructure")]
    public void AddHistoricalJobs_TheRefreshJobsOldNames_LoadItsType(string stored)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddJobs(Config(new()
        {
            ["Database:Provider"] = "Sqlite",
            ["ConnectionStrings:GrifballWebApp"] = "Data Source=historical-renamed-jobs.db",
        }), HistoricalServiceCollectionExtensions.AddHistoricalJobs);
        using var provider = services.BuildServiceProvider();

        Assert.That(provider.GetRequiredService<ITypeLoader>().LoadType(stored), Is.EqualTo(typeof(HistoricalRefreshJob)));
    }
}
