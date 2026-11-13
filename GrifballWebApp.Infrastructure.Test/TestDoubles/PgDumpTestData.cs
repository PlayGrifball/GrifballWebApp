namespace GrifballWebApp.Infrastructure.Test;

/// <summary>A fake pg_dump, shared with GrifballWebApp.Integration.Test (which references this project).</summary>
internal static class PgDumpTestData
{
    /// <summary>
    /// A stand-in for pg_dump: a <c>#!/bin/sh</c> script (so Linux and macOS only: a test using it carries
    /// <c>[Platform(Exclude = "Win")]</c>) that finds its --file and --dbname arguments (as $file and $db) and runs
    /// <paramref name="body"/>. Written to <paramref name="directory"/> (the temp directory by default); the caller
    /// deletes it.
    /// </summary>
    internal static string FakePgDump(string body, string? directory = null)
    {
        var path = Path.Combine(directory ?? Path.GetTempPath(), $"grif-test-pgdump-{Guid.NewGuid():N}.sh");
        File.WriteAllText(path, "#!/bin/sh\nwhile [ $# -gt 0 ]; do if [ \"$1\" = --file ]; then file=\"$2\"; fi; if [ \"$1\" = --dbname ]; then db=\"$2\"; fi; shift; done\n" + body + "\n");
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return path;
    }
}
