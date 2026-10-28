using GeneticSharp;

namespace GrifballWebApp.Test;

/// <summary>
/// A GeneticSharp randomization backed by a seeded <see cref="Random"/>, so tests that drive
/// GeneticSharp operators (which read the static <see cref="RandomizationProvider.Current"/>) are reproducible.
/// </summary>
internal sealed class SeededRandomization(int seed) : RandomizationBase
{
    private readonly Random _random = new(seed);

    public override int GetInt(int min, int max) => _random.Next(min, max);

    public override float GetFloat() => _random.NextSingle();

    public override double GetDouble() => _random.NextDouble();
}
