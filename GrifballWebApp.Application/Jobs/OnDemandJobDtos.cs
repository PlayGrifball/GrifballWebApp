using System.Text.Json.Serialization;

namespace GrifballWebApp.Application.Jobs;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OnDemandJobState
{
    /// <summary>Neither queued nor running: <see cref="OnDemandJobStatusDto.LastRun"/> says how the last one went.</summary>
    Idle,
    /// <summary>Waiting for the worker (it looks every 5 s), or for a run of the same job to finish.</summary>
    Queued,
    Running,
}

/// <summary>Where a page's run of an on-demand job is.</summary>
public class OnDemandJobStatusDto
{
    public OnDemandJobState State { get; init; }
    /// <summary>While running: how far it got (0 to 100) and what it is doing, as the job reports it.</summary>
    public int? Progress { get; init; }
    public string? ProgressMessage { get; init; }
    public DateTime? StartedAt { get; init; }
    /// <summary>The last run that finished (within the history kept, a week).</summary>
    public OnDemandRunDto? LastRun { get; init; }
}

public class OnDemandRunDto
{
    public DateTime FiredAt { get; init; }
    public double DurationMs { get; init; }
    /// <summary>Succeeded, Failed, Cancelled or Skipped.</summary>
    public required string Result { get; init; }
    public string? Error { get; init; }
    /// <summary>What the job reported (its JobRunReport): its summary, and its metrics as JSON. Not sent to pages.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string? Summary { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string? MetricsJson { get; init; }
}
