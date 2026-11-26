namespace GrifballWebApp.Application.Spartan;

/// <summary>Checks a player's Spartan against the render service's catalog: only what it can render passes.</summary>
public static class SpartanChoiceValidator
{
    /// <summary>The problems with the choice, each in a sentence for the player; empty when the render service can render it.</summary>
    public static IReadOnlyList<string> Validate(SpartanChoice choice, SpartanCatalog catalog)
    {
        var problems = new List<string>();
        var core = catalog.Cores.FirstOrDefault(c => string.Equals(c.Name, choice.Core, StringComparison.Ordinal));
        if (core is null)
        {
            problems.Add(catalog.PendingCores.Contains(choice.Core, StringComparer.Ordinal)
                ? $"The core '{choice.Core}' is still being prepared; try again in a few minutes"
                : $"There is no core '{choice.Core}'");
            return problems;
        }

        var named = core.Regions.Where(r => r.Kind == "REGION").ToDictionary(r => r.Key, StringComparer.Ordinal);
        foreach (var (key, name) in choice.Armor ?? new Dictionary<string, string>())
        {
            if (!named.TryGetValue(key, out var region))
                problems.Add($"The core '{core.Name}' has no region '{key}'");
            else if (!region.Pieces.Any(p => string.Equals(p.Name, name, StringComparison.Ordinal)))
                problems.Add($"The region '{key}' of '{core.Name}' has no piece '{name}'");
        }

        var seenRegions = new HashSet<int>();
        foreach (var part in choice.Parts ?? [])
        {
            if (!seenRegions.Add(part.Region))
                problems.Add($"The region {part.Region} is given more than once in the parts");
            else if (!core.Regions.Any(r => r.Region == part.Region && r.Pieces.Any(p => p.Permutation == part.Permutation)))
                problems.Add($"The core '{core.Name}' has no piece {part.Permutation} in the region {part.Region}");
        }

        if (choice.Coating is not null && !catalog.Coatings.Contains(choice.Coating, StringComparer.Ordinal))
            problems.Add($"There is no coating '{choice.Coating}'");
        return problems;
    }
}
