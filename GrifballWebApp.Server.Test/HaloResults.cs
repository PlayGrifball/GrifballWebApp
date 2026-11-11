using Surprenant.Grunt.Models;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// Grunt's results, as its client gives them: a result and no error, or an error and no result. The container's
/// constructor says neither is null, which a real call's result doesn't hold to; the tests make both.
/// </summary>
internal static class HaloResults
{
    internal static HaloApiResultContainer<T, HaloApiErrorContainer> Of<T>(T? result, HaloApiErrorContainer? error)
        // Null where Grunt's own client leaves it null (see the summary)
        => new(result!, error!);
}
