namespace GrifballWebApp.Server.DatabaseBackup;

/// <summary>
/// The backups the backup job made, on the pod that runs the jobs (the worker, or the one pod): one at a time, in the
/// folder DatabaseBackup:Directory (a folder in the temp folder by default), each named by its ID, kept for
/// <see cref="ReadyFor"/> or until the next one is made. An API pod reaches them through the worker
/// (<see cref="BackupDownloads"/>).
/// </summary>
public sealed class BackupFiles : IDisposable
{
    private readonly string _directory;
    private readonly TimeProvider _time;
    private readonly Lock _lock = new();
    private ITimer? _expiry;
    private string? _current;

    /// <summary>How long a made backup waits to be fetched.</summary>
    public TimeSpan ReadyFor { get; }

    public BackupFiles(IConfiguration configuration, TimeProvider time)
    {
        ReadyFor = BackupRuns.ReadyForFrom(configuration);
        _directory = configuration["DatabaseBackup:Directory"] is { Length: > 0 } directory ? directory : Path.Combine(Path.GetTempPath(), "grif-backups");
        _time = time;
        Directory.CreateDirectory(_directory);
        // Left by a run before this process: unreachable now (no run refers to it)
        DiscardLocked();
    }

    /// <summary>Deletes the last backup kept, as the next one starts (one at a time on disk).</summary>
    public void DiscardAll()
    {
        lock (_lock)
            DiscardLocked();
    }

    private void DiscardLocked()
    {
        _expiry?.Dispose();
        _expiry = null;
        _current = null;
        foreach (var file in Directory.EnumerateFiles(_directory))
            File.Delete(file);
    }

    /// <summary>Keeps the made file (moved into the folder) for <see cref="ReadyFor"/>. Returns its ID.</summary>
    public string Keep(string madePath)
    {
        var id = Guid.NewGuid().ToString("N");
        lock (_lock)
        {
            DiscardLocked();
            File.Move(madePath, PathOf(id));
            _current = id;
            _expiry = _time.CreateTimer(_ => DiscardAll(), null, ReadyFor, Timeout.InfiniteTimeSpan);
        }
        return id;
    }

    /// <summary>The kept backup with the ID, opened to read; null when it's gone (expired, or the next one was made).</summary>
    public Stream? Open(string id)
    {
        lock (_lock)
        {
            if (_current != id || !Guid.TryParseExact(id, "N", out _) || !File.Exists(PathOf(id)))
                return null;
            // FileShare.Delete: discarded meanwhile, it's still read to the end.
            return new FileStream(PathOf(id), FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 81920, FileOptions.Asynchronous);
        }
    }

    private string PathOf(string id) => Path.Combine(_directory, id);

    public void Dispose() => _expiry?.Dispose();
}
