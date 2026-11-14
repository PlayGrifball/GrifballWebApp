using System.ComponentModel;

namespace GrifballWebApp.Application.Tools;

/// <summary>The time now, in UTC and as Unix seconds (a host formats it: Discord's &lt;t:…&gt; timestamps, say).</summary>
public sealed record CurrentTimeDto(DateTimeOffset Utc, long UnixSeconds);

/// <summary>The clock, for "today", "this week" and how long until a match.</summary>
public class ClockTools
{
    private readonly TimeProvider _time;

    public ClockTools(TimeProvider time)
    {
        _time = time;
    }

    [Description("The time now, in UTC and as Unix seconds.")]
    public CurrentTimeDto GetCurrentTime()
    {
        var now = _time.GetUtcNow();
        return new CurrentTimeDto(now, now.ToUnixTimeSeconds());
    }
}
