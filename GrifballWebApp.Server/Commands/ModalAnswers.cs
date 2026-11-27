using NetCord;

namespace GrifballWebApp.Server.Commands;

/// <summary>A submitted modal's answers, by their inputs' custom IDs (each input arrives wrapped in a Label).</summary>
public static class ModalAnswers
{
    private static IEnumerable<object> Inputs(IEnumerable<IModalComponent> components)
        => components.OfType<Label>().Select(l => (object)l.Component);

    /// <summary>Whether checkbox <paramref name="customId"/> was ticked.</summary>
    public static bool Checked(IEnumerable<IModalComponent> components, string customId)
        => Inputs(components).OfType<Checkbox>().Where(c => c.CustomId == customId).Any(c => c.Checked);

    /// <summary>The value picked in radio group <paramref name="customId"/>; null when none was.</summary>
    public static string? Picked(IEnumerable<IModalComponent> components, string customId)
        => Inputs(components).OfType<RadioGroup>().Where(g => g.CustomId == customId).Select(g => g.SelectedValue).FirstOrDefault();

    /// <summary>What was typed in text input <paramref name="customId"/>; null when it was left empty.</summary>
    public static string? Text(IEnumerable<IModalComponent> components, string customId)
    {
        var text = Inputs(components).OfType<TextInput>().Where(t => t.CustomId == customId).Select(t => t.Value).FirstOrDefault();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}
