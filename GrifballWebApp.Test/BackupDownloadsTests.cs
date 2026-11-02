using System.Net;
using GrifballWebApp.Server.DatabaseBackup;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace GrifballWebApp.Test;

/// <summary>
/// Where a made backup is kept (BackupFiles, on the pod that runs the jobs) and how the page's download reaches it
/// (BackupDownloads): from this pod, or, from an API pod, through the worker with a token only the app's pods can make.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class BackupDownloadsTests
{
    private static string NewDirectory() => Path.Combine(Path.GetTempPath(), $"grif-backup-files-{Guid.NewGuid():N}");

    private static IConfiguration Configuration(string directory, string? workerUrl = null) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["DatabaseBackup:Directory"] = directory, ["DatabaseBackup:WorkerUrl"] = workerUrl })
        .Build();

    private static string NewMade(string text)
    {
        var path = Path.Combine(Path.GetTempPath(), $"grif-backup-made-{Guid.NewGuid():N}");
        File.WriteAllText(path, text);
        return path;
    }

    private static async Task<string> ReadAsync(Stream stream)
    {
        await using (stream)
        using (var reader = new StreamReader(stream))
            return await reader.ReadToEndAsync();
    }

    [Test]
    public async Task Keeps_one_backup_at_a_time_for_its_time()
    {
        var directory = NewDirectory();
        var time = new FakeTimeProvider();
        using var files = new BackupFiles(Configuration(directory), time);

        var first = files.Keep(NewMade("first"));
        Assert.That(await ReadAsync(files.Open(first)!), Is.EqualTo("first"));
        var second = files.Keep(NewMade("second"));

        Assert.Multiple(async () =>
        {
            Assert.That(files.Open(first), Is.Null, "the next one replaces it");
            Assert.That(await ReadAsync(files.Open(second)!), Is.EqualTo("second"));
            Assert.That(files.Open("../../etc/passwd"), Is.Null, "an ID, not a path");
            Assert.That(Directory.GetFiles(directory), Has.Length.EqualTo(1));
        });

        time.Advance(files.ReadyFor);
        Assert.Multiple(() =>
        {
            Assert.That(files.Open(second), Is.Null, "gone once its time is up");
            Assert.That(Directory.GetFiles(directory), Is.Empty);
        });
        Directory.Delete(directory, recursive: true);
    }

    [Test]
    public void Clears_what_a_run_before_this_process_left()
    {
        var directory = NewDirectory();
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, Guid.NewGuid().ToString("N")), "left over");

        using var files = new BackupFiles(Configuration(directory), new FakeTimeProvider());

        Assert.That(Directory.GetFiles(directory), Is.Empty);
        Directory.Delete(directory, recursive: true);
    }

    private static IDataProtectionProvider Protection() => new ServiceCollection().AddDataProtection().Services.BuildServiceProvider().GetRequiredService<IDataProtectionProvider>();

    [Test]
    public async Task The_worker_hands_a_backup_only_to_a_token_for_it()
    {
        var directory = NewDirectory();
        using var files = new BackupFiles(Configuration(directory), new FakeTimeProvider());
        var id = files.Keep(NewMade("made on the worker"));
        var protection = Protection();
        var protector = protection.CreateProtector("GrifballWebApp.DatabaseBackup.Download").ToTimeLimitedDataProtector();

        HttpRequest Request(string? token)
        {
            var context = new DefaultHttpContext();
            if (token is not null)
                context.Request.Headers[BackupDownloads.TokenHeader] = token;
            return context.Request;
        }

        Assert.Multiple(() =>
        {
            Assert.That(BackupDownloads.Serve(id, Request(null), files, protection), Is.InstanceOf<IStatusCodeHttpResult>().With.Property("StatusCode").EqualTo(401));
            Assert.That(BackupDownloads.Serve(id, Request("forged"), files, protection), Is.InstanceOf<IStatusCodeHttpResult>().With.Property("StatusCode").EqualTo(401));
            Assert.That(BackupDownloads.Serve(id, Request(protector.Protect("another", TimeSpan.FromMinutes(1))), files, protection),
                Is.InstanceOf<IStatusCodeHttpResult>().With.Property("StatusCode").EqualTo(401), "a token for another file");
            Assert.That(BackupDownloads.Serve(id, Request(protector.Protect(id, TimeSpan.FromMinutes(1))), files, protection), Is.InstanceOf<IFileHttpResult>());
        });
        var missing = Guid.NewGuid().ToString("N");
        Assert.That(BackupDownloads.Serve(missing, Request(protector.Protect(missing, TimeSpan.FromMinutes(1))), files, protection),
            Is.InstanceOf<IStatusCodeHttpResult>().With.Property("StatusCode").EqualTo(404));
        Directory.Delete(directory, recursive: true);
    }

    /// <summary>The worker, as an API pod sees it: answers what the test says, and remembers what it was asked.</summary>
    private sealed class FakeWorker(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public HttpRequestMessage? Asked { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Asked = request;
            return Task.FromResult(answer(request));
        }
    }

    private static BackupDownloads ApiPod(FakeWorker worker, IDataProtectionProvider protection, string directory)
    {
        var services = new ServiceCollection();
        services.AddHttpClient(BackupDownloads.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => worker);
        var provider = services.BuildServiceProvider();
        var configuration = Configuration(directory, "http://grif-worker:5295");
        return new BackupDownloads(new BackupFiles(configuration, new FakeTimeProvider()), provider.GetRequiredService<IHttpClientFactory>(), protection, configuration);
    }

    [Test]
    public async Task An_api_pod_fetches_the_backup_from_the_worker_with_a_token_for_it()
    {
        var protection = Protection();
        var worker = new FakeWorker(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("streamed from the worker") });
        var directory = NewDirectory();
        var downloads = ApiPod(worker, protection, directory);

        var stream = await downloads.OpenAsync("abc123");

        Assert.That(await ReadAsync(stream!), Is.EqualTo("streamed from the worker"));
        var token = worker.Asked!.Headers.GetValues(BackupDownloads.TokenHeader).Single();
        Assert.Multiple(() =>
        {
            Assert.That(worker.Asked.RequestUri, Is.EqualTo(new Uri("http://grif-worker:5295/internal/backups/abc123")));
            Assert.That(protection.CreateProtector("GrifballWebApp.DatabaseBackup.Download").ToTimeLimitedDataProtector().Unprotect(token), Is.EqualTo("abc123"));
        });
        Directory.Delete(directory, recursive: true);
    }

    [Test]
    public async Task An_api_pod_finds_no_backup_when_the_worker_has_none()
    {
        var directory = NewDirectory();
        var downloads = ApiPod(new FakeWorker(_ => new HttpResponseMessage(HttpStatusCode.NotFound)), Protection(), directory);

        Assert.That(await downloads.OpenAsync("gone"), Is.Null);
        Directory.Delete(directory, recursive: true);
    }
}
