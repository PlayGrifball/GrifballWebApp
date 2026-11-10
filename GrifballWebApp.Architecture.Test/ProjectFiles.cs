using System.Xml.Linq;

namespace GrifballWebApp.Architecture.Test;

/// <summary>
/// The repository's project files, read as they are checked in: which projects there are and which each one's
/// <c>&lt;ProjectReference&gt;</c>s name. A use the compiler leaves no trace of (a const, nameof, an enum's value, all
/// inlined) still needs the reference, so the rules hold on the project files as well as on the compiled code.
/// </summary>
internal static class ProjectFiles
{
    private static readonly Lazy<string> Root = new(() =>
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
            if (File.Exists(Path.Combine(folder.FullName, "GrifballWebApp.slnx")))
                return folder.FullName;
        throw new InvalidOperationException($"GrifballWebApp.slnx is in no folder above {AppContext.BaseDirectory}");
    });

    /// <summary>The repository's root: the folder above this assembly's with GrifballWebApp.slnx in it.</summary>
    public static string RepositoryRoot => Root.Value;

    /// <summary>Every project in the repository: each top-level folder with a csproj of its own name.</summary>
    public static List<string> All()
        => Directory.GetDirectories(RepositoryRoot)
            .Select(Path.GetFileName)
            .OfType<string>()
            .Where(name => File.Exists(Path.Combine(RepositoryRoot, name, name + ".csproj")))
            .Order(StringComparer.Ordinal)
            .ToList();

    /// <summary>Whether <paramref name="project"/> is a test project (GrifballWebApp.*.Test).</summary>
    public static bool IsTest(string project)
        => project.StartsWith("GrifballWebApp.", StringComparison.Ordinal) && project.EndsWith(".Test", StringComparison.Ordinal);

    /// <summary>The projects <paramref name="project"/>'s csproj references (their file names, without .csproj).</summary>
    public static List<string> References(string project)
        => XDocument.Load(Path.Combine(RepositoryRoot, project, project + ".csproj"))
            .Descendants("ProjectReference")
            .Select(r => Path.GetFileNameWithoutExtension(((string?)r.Attribute("Include") ?? "").Replace('\\', '/')))
            .ToList();

    /// <summary>The references of <paramref name="project"/> to projects other than <paramref name="allowed"/>.</summary>
    public static List<string> Violations(string project, params string[] allowed)
        => References(project).Where(r => !allowed.Contains(r)).Select(r => $"{project}.csproj -> {r}").Order(StringComparer.Ordinal).ToList();
}
