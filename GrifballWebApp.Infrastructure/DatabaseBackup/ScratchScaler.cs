using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace GrifballWebApp.Infrastructure.DatabaseBackup;

/// <summary>Starts and stops the scratch server (backupScratch.mode onDemand), which is stopped between backups.</summary>
public interface IScratchScaler
{
    /// <summary>Starts it and waits until it's ready.</summary>
    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Scales the scratch server's Deployment through the Kubernetes API, as the pod's ServiceAccount (the chart's
/// grif-backend, whose Role can read that Deployment and patch its scale, nothing else): 1 to start it, waiting until
/// its pod is ready; 0 to stop it. The token is read again for each call, since the kubelet rotates it.
/// </summary>
public sealed class KubernetesScratchScaler(HttpClient http, string @namespace, string deployment, Func<string> token, ILogger<KubernetesScratchScaler> logger)
    : IScratchScaler
{
    public const string ServiceAccountFolder = "/var/run/secrets/kubernetes.io/serviceaccount";

    /// <summary>The API server, and the namespace of the Deployment.</summary>
    public Uri? BaseAddress => http.BaseAddress;
    public string Namespace => @namespace;

    /// <summary>How often a starting server's readiness is read, and how long it may take (SQL Server's first start, an image pull).</summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(2);
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The pod's own: the API server from KUBERNETES_SERVICE_HOST and _PORT, trusted by the ServiceAccount's CA, and its
    /// namespace and token from its folder.
    /// </summary>
    public static KubernetesScratchScaler InCluster(string deployment, ILogger<KubernetesScratchScaler> logger, string folder = ServiceAccountFolder,
        string? host = null, string? port = null)
    {
        host ??= Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST") ?? "kubernetes.default.svc";
        port ??= Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_PORT") ?? "443";
        var ca = X509Certificate2.CreateFromPem(File.ReadAllText(Path.Combine(folder, "ca.crt")));
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, certificate, _, _) => certificate is not null && TrustedBy(ca, certificate),
        };
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri($"https://{(host.Contains(':') ? $"[{host}]" : host)}:{port}"),
            // Each call: an API server that doesn't answer fails it in this, not HttpClient's 100 seconds.
            Timeout = TimeSpan.FromSeconds(30),
        };
        var @namespace = File.ReadAllText(Path.Combine(folder, "namespace")).Trim();
        return new KubernetesScratchScaler(http, @namespace, deployment, () => File.ReadAllText(Path.Combine(folder, "token")).Trim(), logger);
    }

    /// <summary>Whether the API server's certificate chains to the cluster's CA, and to it only.</summary>
    public static bool TrustedBy(X509Certificate2 ca, X509Certificate2 certificate)
    {
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(ca);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        return chain.Build(certificate);
    }

    private string DeploymentPath => $"/apis/apps/v1/namespaces/{Uri.EscapeDataString(@namespace)}/deployments/{Uri.EscapeDataString(deployment)}";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await ScaleAsync(1, cancellationToken);
        var deadline = DateTime.UtcNow + Timeout;
        while (true)
        {
            using var request = Request(HttpMethod.Get, DeploymentPath);
            using var response = await http.SendAsync(request, cancellationToken);
            await EnsureAsync(response, "read", cancellationToken);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (json.RootElement.TryGetProperty("status", out var status) && status.TryGetProperty("readyReplicas", out var ready) && ready.GetInt32() >= 1)
            {
                if (logger.IsEnabled(LogLevel.Information))
                    logger.LogInformation("The scratch server {Deployment} is up", deployment);
                return;
            }
            if (DateTime.UtcNow >= deadline)
                throw new TimeoutException($"The scratch server ({deployment}) wasn't ready after {Timeout.TotalMinutes:0} minutes: see its pod's events and logs.");
            await Task.Delay(PollInterval, cancellationToken);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await ScaleAsync(0, cancellationToken);
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Stopped the scratch server {Deployment}", deployment);
    }

    private async Task ScaleAsync(int replicas, CancellationToken cancellationToken)
    {
        using var request = Request(HttpMethod.Patch, DeploymentPath + "/scale");
        request.Content = new StringContent("{\"spec\":{\"replicas\":" + replicas + "}}", Encoding.UTF8);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/merge-patch+json");
        using var response = await http.SendAsync(request, cancellationToken);
        await EnsureAsync(response, $"scale to {replicas}", cancellationToken);
    }

    private HttpRequestMessage Request(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token());
        return request;
    }

    private async Task EnsureAsync(HttpResponseMessage response, string what, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException(
            $"Couldn't {what} the scratch server ({deployment}): the Kubernetes API answered {(int)response.StatusCode}. " +
            $"The backend needs the chart's grif-backend ServiceAccount and its Role, and to reach the API (networkPolicy.backend.apiServerTo). {body}".TrimEnd());
    }
}
