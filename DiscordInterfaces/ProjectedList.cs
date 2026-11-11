using System.Collections;

namespace DiscordInterface.Generated;

/// <summary>
/// A read-only list of another list's items, each made another type as it's read: how a wrapper (DiscordInteractionContextGen.cs)
/// gives the original's collection as its own wrappers without copying it.
/// </summary>
public sealed class ProjectedList<TSource, TResult>(IReadOnlyList<TSource> source, Func<TSource, TResult> project) : IReadOnlyList<TResult>
{
    public TResult this[int index] => project(source[index]);

    public int Count => source.Count;

    public IEnumerator<TResult> GetEnumerator() => source.Select(project).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public static class ProjectedListExtensions
{
    /// <summary><paramref name="source"/>'s items made <typeparamref name="TResult"/> as they're read (<see cref="ProjectedList{TSource, TResult}"/>).</summary>
    public static IReadOnlyList<TResult> Project<TSource, TResult>(this IReadOnlyList<TSource> source, Func<TSource, TResult> project) => new ProjectedList<TSource, TResult>(source, project);
}
