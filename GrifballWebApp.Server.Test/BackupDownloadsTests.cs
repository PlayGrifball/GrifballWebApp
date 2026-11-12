using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using GrifballWebApp.Infrastructure.DatabaseBackup;
using GrifballWebApp.Server.DatabaseBackup;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// BackupDownloadEndpoint (the worker's own pod serving a backup it made, given a real HttpRequest and the token it
/// must carry): BackupFiles/BackupDownloads' own tests (no host) are GrifballWebApp.Infrastructure.Test's
/// BackupDownloadsUnitTests.
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
