using GrifballWebApp.Database;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace GrifballWebApp.Server;

public class GamertagAutocomplete : IAutocompleteProvider<AutocompleteInteractionContext>
{
    private readonly GrifballContext _context;
    public GamertagAutocomplete(GrifballContext context)
    {
        _context = context;
    }
    public ValueTask<IEnumerable<ApplicationCommandOptionChoiceProperties>?> GetChoicesAsync(ApplicationCommandInteractionDataOption option,
        AutocompleteInteractionContext context)
    {
        var input = option.Value!;

        var result = _context.XboxUsers
            .Select(x => x.Gamertag)
            .Where(x => x.Contains(input))
            .Take(25)
            .Select(x => new ApplicationCommandOptionChoiceProperties(x, x));

        return new(result);
    }
}
