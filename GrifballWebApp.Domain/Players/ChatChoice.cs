using GrifballWebApp.Domain.Shared;

namespace GrifballWebApp.Domain.Players;

/// <summary>
/// The chat model a user chose (a configured model's ID), on the site and in a Discord DM: with none, their roles' default. Whether
/// they may still use it is the chat's to check, each question. No foreign key: a choice outlives nothing it needs. Begins through
/// <see cref="Make"/>; changes through <see cref="Choose"/>.
/// </summary>
[AggregateRoot]
public class ChatChoice
{
    /// <summary>The longest model ID kept: a configured model's.</summary>
    public const int ModelMaxLength = 64;

    /// <summary>For EF. A choice begins through <see cref="Make"/>.</summary>
    private ChatChoice() { }

    /// <summary><paramref name="userId"/>'s choice of <paramref name="model"/>. Refuses a blank or too long one.</summary>
    public static ChatChoice Make(int userId, string model)
    {
        var choice = new ChatChoice { UserID = userId };
        choice.Choose(model);
        return choice;
    }

    public int UserID { get; private set; }

    /// <summary>The model's configured ID, at most <see cref="ModelMaxLength"/> characters.</summary>
    public string Model { get; private set; } = null!;

    /// <summary>Another model chosen. Refuses a blank or too long one.</summary>
    public void Choose(string model)
    {
        if (string.IsNullOrWhiteSpace(model) || model.Length > ModelMaxLength)
            throw new ArgumentException($"A chat model ID is 1 to {ModelMaxLength} characters", nameof(model));
        Model = model;
    }
}
