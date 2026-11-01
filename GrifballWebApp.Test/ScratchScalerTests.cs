using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using GrifballWebApp.Server.DatabaseBackup;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrifballWebApp.Test;

/// <summary>KubernetesScratchScaler against a stand-in Kubernetes API, and ScratchScaleDown.</summary>
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

    private static X509Certificate2 Ca(string name, out RSA key)
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

    [TestCase("10.43.0.1", "443", "https://10.43.0.1/")]
    [TestCase("fd00:10:43::1", "6443", "https://[fd00:10:43::1]:6443/")]
    public void In_cluster_it_takes_the_pods_API_server_namespace_and_token(string host, string port, string address)
    {
        var folder = Directory.CreateTempSubdirectory("grif-scratch-test-");
        try
        {
            using var ca = Ca("cluster", out var key);
            key.Dispose();
            File.WriteAllText(Path.Combine(folder.FullName, "ca.crt"), ca.ExportCertificatePem());
            File.WriteAllText(Path.Combine(folder.FullName, "namespace"), "grif-prod\n");
            File.WriteAllText(Path.Combine(folder.FullName, "token"), "t");

            var scaler = KubernetesScratchScaler.InCluster("grif-backup-scratch", NullLogger<KubernetesScratchScaler>.Instance, folder.FullName, host, port);

            Assert.That(scaler.BaseAddress, Is.EqualTo(new Uri(address)));
            Assert.That(scaler.Namespace, Is.EqualTo("grif-prod"));
        }
        finally
        {
            folder.Delete(recursive: true);
        }
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

    [Test]
    public async Task At_startup_the_scratch_server_is_stopped_and_a_failure_only_logged()
    {
        var scaler = new RecordingScaler();
        var scaleDown = new ScratchScaleDown(scaler, NullLogger<ScratchScaleDown>.Instance);
        await scaleDown.StartAsync(CancellationToken.None);
        await scaleDown.ExecuteTask!;
        Assert.That(scaler.Calls, Is.EqualTo(new[] { "stop" }));

        var failing = new ScratchScaleDown(new RecordingScaler(new InvalidOperationException("no API")), NullLogger<ScratchScaleDown>.Instance);
        await failing.StartAsync(CancellationToken.None);
        await failing.ExecuteTask!;
        await failing.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task At_startup_an_API_that_never_answers_holds_nothing_up_and_is_given_up_on()
    {
        var hanging = new HangingScaler();
        var scaleDown = new ScratchScaleDown(hanging, NullLogger<ScratchScaleDown>.Instance) { Timeout = TimeSpan.FromMilliseconds(100) };

        var start = scaleDown.StartAsync(CancellationToken.None);

        Assert.That(start.IsCompleted, Is.True, "startup goes on while the API is called");
        await scaleDown.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.That(hanging.Cancelled, Is.True);
    }

    /// <summary>An API server that never answers: the call ends only when it's cancelled.</summary>
    private sealed class HangingScaler : IScratchScaler
    {
        public bool Cancelled { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            finally
            {
                Cancelled = cancellationToken.IsCancellationRequested;
            }
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
