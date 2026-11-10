using Microsoft.Extensions.Logging.Abstractions;
using GrifballWebApp.Infrastructure.DatabaseBackup;

namespace GrifballWebApp.Integration.Test;

/// <summary>KubernetesScratchScaler in a pod: it reads the API server's CA, its namespace and its token from files.</summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class ScratchScalerInClusterTests
{
    [TestCase("10.43.0.1", "443", "https://10.43.0.1/")]
    [TestCase("fd00:10:43::1", "6443", "https://[fd00:10:43::1]:6443/")]
    public void In_cluster_it_takes_the_pods_API_server_namespace_and_token(string host, string port, string address)
    {
        var folder = Directory.CreateTempSubdirectory("grif-scratch-test-");
        try
        {
            using var ca = TestCertificates.Ca("cluster", out var key);
            key.Dispose();
            File.WriteAllText(Path.Combine(folder.FullName, "ca.crt"), ca.ExportCertificatePem());
            File.WriteAllText(Path.Combine(folder.FullName, "namespace"), "grif-prod\n");
            File.WriteAllText(Path.Combine(folder.FullName, "token"), "t");

            var scaler = KubernetesScratchScaler.InCluster("grif-backup-scratch", NullLogger<KubernetesScratchScaler>.Instance, folder.FullName, host, port);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(scaler.BaseAddress, Is.EqualTo(new Uri(address)));
                Assert.That(scaler.Namespace, Is.EqualTo("grif-prod"));
            }
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }
}
