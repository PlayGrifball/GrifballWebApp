using GrifballWebApp.Domain.Shared;
using System.Security.Claims;
using System.Text.Json;
using Quartz;
using Quartz.Extensibility;

namespace GrifballWebApp.Server.Jobs;

/// <summary>A job's input that says who queued it: the job runs as them, so what it saves is audited as theirs.</summary>
public interface IRequestedByUser
{
    /// <summary>The user who queued the run; null for one nobody did (a schedule).</summary>
    int? RequestedByUserID { get; }
}

/// <summary>
/// Who a job runs as. A run someone started (an on-demand job queued from a page, its input an <see cref="IRequestedByUser"/>,
/// or "run now" on the Jobs page, its trigger carrying <see cref="DataKey"/>) runs as them: before the job is even built,
/// its scope's <see cref="ICurrentUserService"/> is set to them (<see cref="Apply"/>, Quartz's job scope hook), so the audit
/// columns of everything it saves name them, as they did when this work ran in their request. A scheduled run has none,
/// and its saves name nobody.
/// </summary>
public static class JobUser
{
    /// <summary>The trigger data key "run now" puts the user who pressed it under.</summary>
    public const string DataKey = "grif.requestedByUserId";

    /// <summary>The signed-in user's ID, from their claims; null when there's none.</summary>
    public static int? IdOf(ClaimsPrincipal? user) =>
        user?.Identity?.IsAuthenticated == true && int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    /// <summary>The user a firing runs as: from its trigger's data, else its input; null for none.</summary>
    public static int? Of(TriggerFiredBundle bundle) => Of(bundle.Trigger.JobDataMap) ?? Of(bundle.JobDetail.JobDataMap);

    /// <summary>The user in the data: <see cref="DataKey"/>, else the stored input's <see cref="IRequestedByUser.RequestedByUserID"/>.</summary>
    public static int? Of(JobDataMap data)
    {
        if (data.TryGetValue(DataKey, out var value) && value is not null && int.TryParse(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture), out var id))
            return id;
        if (data.TryGetValue(SchedulerConstants.JobInput, out var input) && input is string { Length: > 0 } json)
        {
            try
            {
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in document.RootElement.EnumerateObject())
                    {
                        if (string.Equals(property.Name, nameof(IRequestedByUser.RequestedByUserID), StringComparison.OrdinalIgnoreCase)
                            && property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt32(out var fromInput))
                            return fromInput;
                    }
                }
            }
            catch (JsonException)
            {
                // Not JSON: an input with no user
            }
        }
        return null;
    }

    /// <summary>Quartz's job scope hook: the job's scope runs as the user who started the run, if anyone did.</summary>
    public static void Apply(IServiceScope scope, TriggerFiredBundle bundle)
    {
        if (Of(bundle) is { } id)
            scope.ServiceProvider.GetService<ICurrentUserService>()?.SetCurrentUserId(id);
    }
}
