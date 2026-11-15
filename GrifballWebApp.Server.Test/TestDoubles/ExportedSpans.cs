using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// What an OpenTelemetry tracer, sampling as the app's does (parent-based, else always), would export of the Discord bot's spans
/// (<see cref="MessageCreateHandler.ActivitySourceName"/>) and of a test's stand-in for a call made under them (<see cref="Call"/>):
/// for the bot's tests of which traces leave the app.
/// </summary>
internal sealed class ExportedSpans : IDisposable
{
    private const string CallName = "GrifballWebApp.Server.Test.Call";

    /// <summary>A call's span (the HTTP client's, say) under whatever is current.</summary>
    public static readonly ActivitySource Call = new(CallName);

    private readonly List<Activity> _exported = [];
    private readonly TracerProvider _tracing;

    public ExportedSpans()
        => _tracing = Sdk.CreateTracerProviderBuilder()
            .AddSource(MessageCreateHandler.ActivitySourceName, CallName)
            .AddProcessor(new SimpleActivityExportProcessor(new Exporter(_exported)))
            .Build()!;

    /// <summary>The spans exported of one trace: other tests' are in others.</summary>
    public List<Activity> Of(ActivityTraceId trace)
    {
        lock (_exported)
            return _exported.Where(a => a.TraceId == trace).ToList();
    }

    public void Dispose() => _tracing.Dispose();

    private sealed class Exporter(List<Activity> exported) : BaseExporter<Activity>
    {
        public override ExportResult Export(in Batch<Activity> batch)
        {
            lock (exported)
            {
                foreach (var activity in batch)
                    exported.Add(activity);
            }
            return ExportResult.Success;
        }
    }
}
