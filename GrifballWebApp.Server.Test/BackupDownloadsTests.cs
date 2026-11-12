using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using GrifballWebApp.Infrastructure.DatabaseBackup;
using GrifballWebApp.Server.DatabaseBackup;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// Where a made backup is kept (BackupFiles, on the pod that runs the jobs) and how the page's download reaches it
/// (BackupDownloads): from this pod, or, from an API pod, through the worker with a token only the app's pods can make.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class BackupDownloadsTests
{
    private static string NewDirectory() => Path.Combine(Path.GetTempPath(), $"grif-backup-files-{Guid.NewGuid():N}");

    private static IConfiguration Configuration(string directory) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["DatabaseBackup:Directory"] = directory })
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
    public async Task Keeps_one_backup_a_format_each_for_its_time()
    {
        var directory = NewDirectory();
        var time = new FakeTimeProvider();
        using var files = new BackupFiles(Configuration(directory), time);

        var first = files.Keep(BackupFormat.Sqlite, NewMade("first"));
        Assert.That(await ReadAsync(files.Open(first)!), Is.EqualTo("first"));
        time.Advance(TimeSpan.FromMinutes(30));
        var script = files.Keep(BackupFormat.Postgres, NewMade("script"));
        var second = files.Keep(BackupFormat.Sqlite, NewMade("second"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(files.Open(first), Is.Null, "the next of its format replaces it");
            Assert.That(await ReadAsync(files.Open(second)!), Is.EqualTo("second"));
            Assert.That(await ReadAsync(files.Open(script)!), Is.EqualTo("script"), "another format's is kept beside it");
            Assert.That(files.Open("../../etc/passwd"), Is.Null, "an ID, not a path");
            Assert.That(Directory.GetFiles(directory), Has.Length.EqualTo(2));
        }

        // The first's timer (replaced) fires at 60 minutes: it leaves the newer one alone
        time.Advance(TimeSpan.FromMinutes(30));
        Assert.That(files.Open(second), Is.Not.Null);
        time.Advance(TimeSpan.FromMinutes(30));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(files.Open(second), Is.Null, "gone once its time is up");
            Assert.That(files.Open(script), Is.Null);
            Assert.That(Directory.GetFiles(directory), Is.Empty);
        }
        files.Discard(BackupFormat.Dump);
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
        var id = files.Keep(BackupFormat.Sqlite, NewMade("made on the worker"));
        var protection = Protection();
        var protector = protection.CreateProtector("GrifballWebApp.DatabaseBackup.Download").ToTimeLimitedDataProtector();

        HttpRequest Request(string? token)
        {
            var context = new DefaultHttpContext();
            if (token is not null)
                context.Request.Headers[BackupDownloads.TokenHeader] = token;
            return context.Request;
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(BackupDownloadEndpoint.Serve(id, Request(null), files, protection), Is.InstanceOf<IStatusCodeHttpResult>().With.Property("StatusCode").EqualTo(401));
            Assert.That(BackupDownloadEndpoint.Serve(id, Request("forged"), files, protection), Is.InstanceOf<IStatusCodeHttpResult>().With.Property("StatusCode").EqualTo(401));
            Assert.That(BackupDownloadEndpoint.Serve(id, Request(protector.Protect("another", TimeSpan.FromMinutes(1))), files, protection),
                Is.InstanceOf<IStatusCodeHttpResult>().With.Property("StatusCode").EqualTo(401), "a token for another file");
            Assert.That(BackupDownloadEndpoint.Serve(id, Request(protector.Protect(id, TimeSpan.FromMinutes(1))), files, protection), Is.InstanceOf<IFileHttpResult>());
        }
        var missing = Guid.NewGuid().ToString("N");
        Assert.That(BackupDownloadEndpoint.Serve(missing, Request(protector.Protect(missing, TimeSpan.FromMinutes(1))), files, protection),
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
        return new BackupDownloads(new BackupFiles(Configuration(directory), new FakeTimeProvider()), provider.GetRequiredService<IHttpClientFactory>(), protection);
    }

    [Test]
    public async Task An_api_pod_fetches_the_backup_from_the_worker_with_a_token_for_it()
    {
        var protection = Protection();
        var worker = new FakeWorker(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("streamed from the worker") });
        var directory = NewDirectory();
        var downloads = ApiPod(worker, protection, directory);

        // Made by one of the workers: fetched from that one, by the address its run recorded
        var stream = await downloads.OpenAsync(new ReadyBackup("abc123", "b.sql", "application/sql", "http://10.42.0.7:5295"));

        // Read as the response writes it, and the rest of a stream's members as one that only reads forward
        using (Assert.EnterMultipleScope())
        {
            Assert.That((stream!.CanRead, stream.CanSeek, stream.CanWrite), Is.EqualTo((true, false, false)));
            Assert.That(() => stream.Length, Throws.InstanceOf<NotSupportedException>());
            Assert.That(() => stream.Position, Throws.InstanceOf<NotSupportedException>());
            Assert.That(() => stream.Position = 1, Throws.InstanceOf<NotSupportedException>());
            Assert.That(() => stream.Seek(0, SeekOrigin.Begin), Throws.InstanceOf<NotSupportedException>());
            Assert.That(() => stream.SetLength(1), Throws.InstanceOf<NotSupportedException>());
            Assert.That(() => stream.Write(new byte[1], 0, 1), Throws.InstanceOf<NotSupportedException>());
        }
#pragma warning disable VSTHRD103 // The stream's sync members are under test, as its async ones
        stream!.Flush();
#pragma warning restore VSTHRD103
        var first = new byte[8];
        var next = new byte[8];
        using (Assert.EnterMultipleScope())
        {
#pragma warning disable VSTHRD103 // The sync Read is under test
            Assert.That(stream.Read(first, 0, 8), Is.EqualTo(8));
#pragma warning restore VSTHRD103
#pragma warning disable CA1835 // The stream's array overload is the one under test (the memory one is read below)
            Assert.That(await stream.ReadAsync(next, 0, 8), Is.EqualTo(8));
#pragma warning restore CA1835
            Assert.That(System.Text.Encoding.UTF8.GetString(first) + System.Text.Encoding.UTF8.GetString(next) + await ReadAsync(stream), Is.EqualTo("streamed from the worker"));
        }
        var token = worker.Asked!.Headers.GetValues(BackupDownloads.TokenHeader).Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(worker.Asked.RequestUri, Is.EqualTo(new Uri("http://10.42.0.7:5295/internal/backups/abc123")));
            Assert.That(protection.CreateProtector("GrifballWebApp.DatabaseBackup.Download").ToTimeLimitedDataProtector().Unprotect(token), Is.EqualTo("abc123"));
        }
        Directory.Delete(directory, recursive: true);
    }

    [Test]
    public async Task With_several_workers_an_api_pod_fetches_the_backup_from_the_one_that_made_it()
    {
        // Two workers: each has only the backups it made
        var worker = new FakeWorker(request => request.RequestUri!.Host == "10.42.0.8"
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("made on the second worker") }
            : new HttpResponseMessage(HttpStatusCode.NotFound));
        var directory = NewDirectory();
        var downloads = ApiPod(worker, Protection(), directory);

        var stream = await downloads.OpenAsync(new ReadyBackup("def456", "b.sql", "application/sql", "http://10.42.0.8:5295"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await ReadAsync(stream!), Is.EqualTo("made on the second worker"));
            Assert.That(worker.Asked!.RequestUri, Is.EqualTo(new Uri("http://10.42.0.8:5295/internal/backups/def456")), "Not the other worker");
        }
        Directory.Delete(directory, recursive: true);
    }

    [Test]
    public async Task An_api_pod_finds_no_backup_when_the_worker_has_none()
    {
        var directory = NewDirectory();
        var downloads = ApiPod(new FakeWorker(_ => new HttpResponseMessage(HttpStatusCode.NotFound)), Protection(), directory);

        Assert.That(await downloads.OpenAsync(new ReadyBackup("gone", "b.sql", "application/sql", "http://10.42.0.7:5295")), Is.Null);
        Directory.Delete(directory, recursive: true);
    }

    /// <summary>The worker that made it restarted: nobody at its address, another pod there now (401), or no answer in time.</summary>
    [Test]
    public async Task An_api_pod_finds_no_backup_when_the_worker_that_made_it_is_gone()
    {
        var backup = new ReadyBackup("abc123", "b.sql", "application/sql", "http://10.42.0.7:5295");
        Func<HttpRequestMessage, HttpResponseMessage>[] gone =
        [
            _ => throw new HttpRequestException("Connection refused"),
            _ => new HttpResponseMessage(HttpStatusCode.Unauthorized),
            _ => throw new TaskCanceledException("The connect timed out"),
        ];
        foreach (var answer in gone)
        {
            var directory = NewDirectory();
            var downloads = ApiPod(new FakeWorker(answer), Protection(), directory);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(await downloads.OpenAsync(backup), Is.Null);
                Assert.That(await downloads.ExistsAsync(backup), Is.False);
            }
            Directory.Delete(directory, recursive: true);
        }

        // Cancelled by the caller (the browser went away): that's not the worker being gone.
        var cancelledDirectory = NewDirectory();
        var cancelled = ApiPod(new FakeWorker(_ => throw new TaskCanceledException()), Protection(), cancelledDirectory);
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();
        await Assert.CatchAsync<OperationCanceledException>(() => cancelled.OpenAsync(backup, cancel.Token));
        Directory.Delete(cancelledDirectory, recursive: true);
    }

    [Test]
    public async Task An_api_pod_asks_the_worker_whether_the_backup_is_there_without_fetching_it()
    {
        var directory = NewDirectory();
        var worker = new FakeWorker(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var downloads = ApiPod(worker, Protection(), directory);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await downloads.ExistsAsync(new ReadyBackup("abc123", "b.sql", "application/sql", "http://10.42.0.7:5295")), Is.True);
            Assert.That(worker.Asked!.Method, Is.EqualTo(HttpMethod.Head));
        }
        Directory.Delete(directory, recursive: true);
    }

    [Test]
    public async Task One_pod_looks_for_the_backup_itself_and_the_worker_answers_a_HEAD_without_the_file()
    {
        var directory = NewDirectory();
        using var files = new BackupFiles(Configuration(directory), new FakeTimeProvider());
        var id = files.Keep(BackupFormat.Sqlite, NewMade("made here"));
        var protection = Protection();
        var downloads = new BackupDownloads(files, new ServiceCollection().AddHttpClient().BuildServiceProvider().GetRequiredService<IHttpClientFactory>(), protection);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await downloads.ExistsAsync(new ReadyBackup(id, "b.sqlite", "application/x-sqlite3", null)), Is.True);
            Assert.That(await downloads.ExistsAsync(new ReadyBackup("other", "b.sqlite", "application/x-sqlite3", null)), Is.False);
        }

        var protector = protection.CreateProtector("GrifballWebApp.DatabaseBackup.Download").ToTimeLimitedDataProtector();
        HttpRequest Head(string fileId)
        {
            var context = new DefaultHttpContext();
            context.Request.Method = HttpMethods.Head;
            context.Request.Headers[BackupDownloads.TokenHeader] = protector.Protect(fileId, TimeSpan.FromMinutes(1));
            return context.Request;
        }
        using (Assert.EnterMultipleScope())
        {
            Assert.That(BackupDownloadEndpoint.Serve(id, Head(id), files, protection), Is.InstanceOf<IStatusCodeHttpResult>().With.Property("StatusCode").EqualTo(200));
            Assert.That(BackupDownloadEndpoint.Serve("other", Head("other"), files, protection), Is.InstanceOf<IStatusCodeHttpResult>().With.Property("StatusCode").EqualTo(404));
        }

        files.Discard(BackupFormat.Sqlite);
        Assert.That(files.Exists(id), Is.False);
        Directory.Delete(directory, recursive: true);
    }
}
