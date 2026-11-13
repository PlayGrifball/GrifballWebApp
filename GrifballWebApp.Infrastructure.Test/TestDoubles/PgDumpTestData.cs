namespace GrifballWebApp.Infrastructure.Test;

/// <summary>A fake pg_dump, shared with GrifballWebApp.Integration.Test (which references this project).</summary>
internal static class PgDumpTestData
{
    /// <summary>A stand-in for pg_dump: a shell script that finds its --file and --dbname arguments (as $file and $db) and runs <paramref name="body"/>.
    /// The caller deletes it.</summary>
    internal static string FakePgDump(string body)
    {
        var path = Path.Combine(Path.GetTempPath(), $"grif-test-pgdump-{Guid.NewGuid():N}.sh");
        File.WriteAllText(path, "#!/bin/sh\nwhile [ $# -gt 0 ]; do if [ \"$1\" = --file ]; then file=\"$2\"; fi; if [ \"$1\" = --dbname ]; then db=\"$2\"; fi; shift; done\n" + body + "\n");
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return path;
    }
}
