using Microsoft.Extensions.Configuration;

namespace GrifballWebApp.Infrastructure.DatabaseBackup;

/// <summary>
/// The backups the backup job made, on the pod that runs the jobs (the worker, or the one pod): one per format, in the
/// folder DatabaseBackup:Directory (a folder in the temp folder by default), each named by its ID, kept for
/// <see cref="ReadyFor"/> or until the next one of its format is made. An API pod reaches them through the worker
/// (<see cref="BackupDownloads"/>).
/// </summary>
public sealed class BackupFiles : IDisposable
{
    private sealed record Kept(string Id, ITimer Expiry);

    private readonly string _directory;
    private readonly TimeProvider _time;
    private readonly Lock _lock = new();
    private readonly Dictionary<BackupFormat, Kept> _kept = [];

    /// <summary>How long a made backup waits to be fetched.</summary>
    public TimeSpan ReadyFor { get; }

    public BackupFiles(IConfiguration configuration, TimeProvider time)
    {
        ReadyFor = BackupRuns.ReadyForFrom(configuration);
        _directory = configuration["DatabaseBackup:Directory"] is { Length: > 0 } directory ? directory : Path.Combine(Path.GetTempPath(), "grif-backups");
        _time = time;
        Directory.CreateDirectory(_directory);
        // Left by a run before this process: unreachable now (no run refers to it)
        foreach (var file in Directory.EnumerateFiles(_directory))
            File.Delete(file);
    }

    /// <summary>Deletes the backup of the format kept, as the next one of its format starts.</summary>
    public void Discard(BackupFormat format)
    {
        lock (_lock)
            DiscardLocked(format);
    }

    private void DiscardLocked(BackupFormat format)
    {
        if (!_kept.Remove(format, out var kept))
            return;
        kept.Expiry.Dispose();
        File.Delete(PathOf(kept.Id));
    }

    /// <summary>Keeps the made file (moved into the folder) as the format's backup, for <see cref="ReadyFor"/>. Returns its ID.</summary>
    public string Keep(BackupFormat format, string madePath)
    {
        var id = Guid.NewGuid().ToString("N");
        lock (_lock)
        {
            DiscardLocked(format);
            File.Move(madePath, PathOf(id));
            _kept[format] = new Kept(id, _time.CreateTimer(_ => Expire(format, id), null, ReadyFor, Timeout.InfiniteTimeSpan));
        }
        return id;
    }

    /// <summary>The format's backup gone once its time is up, unless a newer one replaced it meanwhile.</summary>
    private void Expire(BackupFormat format, string id)
    {
        lock (_lock)
        {
            if (_kept.TryGetValue(format, out var kept) && kept.Id == id)
                DiscardLocked(format);
        }
    }

    /// <summary>Whether the kept backup with the ID is still here (not expired, replaced, or lost to a restart).</summary>
    public bool Exists(string id)
    {
        lock (_lock)
            return _kept.Values.Any(k => k.Id == id) && File.Exists(PathOf(id));
    }

    /// <summary>The kept backup with the ID, opened to read; null when it's gone (expired, or the next of its format was made).</summary>
    public Stream? Open(string id)
    {
        lock (_lock)
        {
            if (!_kept.Values.Any(k => k.Id == id) || !File.Exists(PathOf(id)))
                return null;
            // FileShare.Delete: discarded meanwhile, it's still read to the end.
            return new FileStream(PathOf(id), FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 81920, FileOptions.Asynchronous);
        }
    }

    private string PathOf(string id) => Path.Combine(_directory, id);

    public void Dispose()
    {
        lock (_lock)
            foreach (var kept in _kept.Values)
                kept.Expiry.Dispose();
    }
}
