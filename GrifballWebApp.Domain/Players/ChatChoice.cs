using GrifballWebApp.Domain.Shared;

namespace GrifballWebApp.Domain.Players;

/// <summary>
/// A user's chat choices, on the site and in a Discord DM: the model they chose (a configured model's ID; with none, their roles'
/// default), whether they may still use it the chat's to check, each question; and whether a change the chat makes for them runs
/// without asking (<see cref="AutoApprove"/>). No foreign key: a choice outlives nothing it needs. Begins through <see cref="Make(int)"/>;
/// changes through <see cref="Choose"/> and <see cref="SetAutoApprove"/>.
/// </summary>
[AggregateRoot]
public class ChatChoice
{
    /// <summary>The longest model ID kept: a configured model's.</summary>
    public const int ModelMaxLength = 64;

    /// <summary>For EF. A choice begins through <see cref="Make(int)"/>.</summary>
    private ChatChoice() { }

    /// <summary><paramref name="userId"/>'s choices, none made yet: their roles' default model, and changes waiting for them.</summary>
    public static ChatChoice Make(int userId) => new() { UserID = userId };

    /// <summary><paramref name="userId"/>'s choice of <paramref name="model"/>. Refuses a blank or too long one.</summary>
    public static ChatChoice Make(int userId, string model)
    {
        var choice = Make(userId);
        choice.Choose(model);
        return choice;
    }

    public int UserID { get; private set; }

    /// <summary>The model's configured ID, at most <see cref="ModelMaxLength"/> characters; null, none chosen.</summary>
    public string? Model { get; private set; }

    /// <summary>Whether a change the chat makes for them runs at once, without asking them first; false (the default), it waits for their yes.</summary>
    public bool AutoApprove { get; private set; }

    /// <summary>They chose whether a change runs without asking them (<see cref="AutoApprove"/>).</summary>
    public void SetAutoApprove(bool on) => AutoApprove = on;

    /// <summary>Another model chosen. Refuses a blank or too long one.</summary>
    public void Choose(string model)
    {
        if (string.IsNullOrWhiteSpace(model) || model.Length > ModelMaxLength)
            throw new ArgumentException($"A chat model ID is 1 to {ModelMaxLength} characters", nameof(model));
        Model = model;
    }
}
