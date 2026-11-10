using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging.Abstractions;
using GrifballWebApp.Infrastructure.DatabaseBackup;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>KubernetesScratchScaler against a stand-in Kubernetes API.</summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class ScratchScalerTests
{
    /// <summary>The API: each request recorded with its body, answered in turn (the last answer again once they run out).</summary>
    private sealed class Api(params (HttpStatusCode Status, string Body)[] answers) : HttpMessageHandler
    {
        public readonly List<(HttpMethod Method, string Path, string? Authorization, string? ContentType, string? Body)> Requests = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.Method, request.RequestUri!.AbsolutePath, request.Headers.Authorization?.ToString(),
                request.Content?.Headers.ContentType?.MediaType, request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));
            var (status, body) = answers[Math.Min(Requests.Count - 1, answers.Length - 1)];
            return new HttpResponseMessage(status) { Content = new StringContent(body) };
        }
    }

    private static KubernetesScratchScaler Scaler(Api api, TimeSpan? timeout = null)
    {
        var tokens = 0;
        return new KubernetesScratchScaler(new HttpClient(api) { BaseAddress = new Uri("https://api.cluster:6443") }, "grif-x", "grif-backup-scratch",
            () => $"token-{++tokens}", NullLogger<KubernetesScratchScaler>.Instance)
        {
            PollInterval = TimeSpan.FromMilliseconds(1),
            Timeout = timeout ?? TimeSpan.FromMinutes(1),
        };
    }

    private const string Deployment = "/apis/apps/v1/namespaces/grif-x/deployments/grif-backup-scratch";

    [Test]
    public async Task Start_scales_to_one_and_waits_until_its_pod_is_ready()
    {
        var api = new Api((HttpStatusCode.OK, "{}"), (HttpStatusCode.OK, """{"status":{}}"""), (HttpStatusCode.OK, """{"status":{"readyReplicas":0}}"""),
            (HttpStatusCode.OK, """{"status":{"readyReplicas":1}}"""));

        await Scaler(api).StartAsync(CancellationToken.None);

        Assert.That(api.Requests, Has.Count.EqualTo(4));
        Assert.That(api.Requests[0], Is.EqualTo((HttpMethod.Patch, Deployment + "/scale", "Bearer token-1", "application/merge-patch+json", """{"spec":{"replicas":1}}""")));
        Assert.That(api.Requests.Skip(1).Select(r => (r.Method, r.Path)), Is.All.EqualTo((HttpMethod.Get, Deployment)));
        // Read again for each call: the kubelet rotates it.
        Assert.That(api.Requests[3].Authorization, Is.EqualTo("Bearer token-4"));
    }

    [Test]
    public async Task Start_gives_up_on_a_server_that_never_gets_ready()
    {
        var api = new Api((HttpStatusCode.OK, "{}"), (HttpStatusCode.OK, """{"status":{"replicas":1}}"""));

        var e = await Assert.ThrowsAsync<TimeoutException>(() => Scaler(api, TimeSpan.Zero).StartAsync(CancellationToken.None));

        Assert.That(e!.Message, Does.Contain("grif-backup-scratch").And.Contain("pod's events"));
    }

    [Test]
    public async Task Stop_scales_to_zero()
    {
        var api = new Api((HttpStatusCode.OK, "{}"));

        await Scaler(api).StopAsync(CancellationToken.None);

        Assert.That(api.Requests.Single(), Is.EqualTo((HttpMethod.Patch, Deployment + "/scale", "Bearer token-1", "application/merge-patch+json", """{"spec":{"replicas":0}}""")));
    }

    [Test]
    public async Task Says_what_the_API_refused_and_what_it_needs()
    {
        var api = new Api((HttpStatusCode.Forbidden, """{"reason":"Forbidden"}"""));

        var e = await Assert.ThrowsAsync<InvalidOperationException>(() => Scaler(api).StopAsync(CancellationToken.None));

        Assert.That(e!.Message, Does.StartWith("Couldn't scale to 0 the scratch server (grif-backup-scratch): the Kubernetes API answered 403."));
        Assert.That(e.Message, Does.Contain("ServiceAccount").And.Contain("apiServerTo").And.EndWith("""{"reason":"Forbidden"}"""));
    }

    [Test]
    public async Task Says_when_it_cant_read_the_Deployment()
    {
        var api = new Api((HttpStatusCode.OK, "{}"), (HttpStatusCode.NotFound, ""));

        var e = await Assert.ThrowsAsync<InvalidOperationException>(() => Scaler(api).StartAsync(CancellationToken.None));

        Assert.That(e!.Message, Does.StartWith("Couldn't read the scratch server").And.EndWith("(networkPolicy.backend.apiServerTo)."));
    }

    /// <summary>For ScratchScalerInClusterTests too.</summary>
    internal static X509Certificate2 Ca(string name, out RSA key)
    {
        key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={name}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }

    [Test]
    public void Trusts_the_API_servers_certificate_only_from_the_clusters_CA()
    {
        using var ca = Ca("cluster", out var caKey);
        using var other = Ca("other", out var otherKey);
        using var serverKey = RSA.Create(2048);
        var request = new CertificateRequest("CN=kubernetes", serverKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var server = request.Create(ca, DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddHours(1), [1, 2, 3, 4]);

        Assert.That(KubernetesScratchScaler.TrustedBy(ca, server), Is.True);
        Assert.That(KubernetesScratchScaler.TrustedBy(other, server), Is.False);
        caKey.Dispose();
        otherKey.Dispose();
    }

    private sealed class RecordingScaler(Exception? stopFails = null) : IScratchScaler
    {
        public readonly List<string> Calls = [];

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Calls.Add("start");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Calls.Add("stop");
            return stopFails is null ? Task.CompletedTask : Task.FromException(stopFails);
        }
    }

    /// <summary>For DatabaseBackupScratchTests.</summary>
    internal static IScratchScaler Recording(out List<string> calls)
    {
        var scaler = new RecordingScaler();
        calls = scaler.Calls;
        return scaler;
    }
}
