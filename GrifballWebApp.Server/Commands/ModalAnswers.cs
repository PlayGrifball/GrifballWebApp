using NetCord;

namespace GrifballWebApp.Server.Commands;

/// <summary>A submitted modal's answers, by their inputs' custom IDs (each input arrives wrapped in a Label).</summary>
public static class ModalAnswers
{
    private static IEnumerable<object> Inputs(IEnumerable<IModalComponent> components)
        => components.OfType<Label>().Select(l => (object)l.Component);

    /// <summary>Whether checkbox <paramref name="customId"/> was ticked.</summary>
    public static bool Checked(IEnumerable<IModalComponent> components, string customId)
        => Inputs(components).OfType<Checkbox>().Any(c => c.CustomId == customId && c.Checked);

    /// <summary>The value picked in radio group <paramref name="customId"/>; null when none was.</summary>
    public static string? Picked(IEnumerable<IModalComponent> components, string customId)
        => Inputs(components).OfType<RadioGroup>().FirstOrDefault(g => g.CustomId == customId)?.SelectedValue;

    /// <summary>What was typed in text input <paramref name="customId"/>; null when it was left empty.</summary>
    public static string? Text(IEnumerable<IModalComponent> components, string customId)
        => Inputs(components).OfType<TextInput>().FirstOrDefault(t => t.CustomId == customId)?.Value is { } text && !string.IsNullOrWhiteSpace(text) ? text : null;
}
