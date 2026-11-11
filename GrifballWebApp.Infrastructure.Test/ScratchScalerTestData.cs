using GrifballWebApp.Infrastructure.DatabaseBackup;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>A scratch scaler that records its calls.</summary>
internal static class ScratchScalerTestData
{
    internal sealed class RecordingScaler(Exception? stopFails = null) : IScratchScaler
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
