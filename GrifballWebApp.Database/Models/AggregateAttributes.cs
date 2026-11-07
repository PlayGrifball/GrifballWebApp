namespace GrifballWebApp.Database.Models;

/// <summary>
/// A model type that is part of an aggregate, not a root of its own: its internal constructors, setters and methods are
/// for <see cref="Root"/> and the aggregate's other parts alone (AggregateBoundaryTests), and its DbSet is internal.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AggregatePartAttribute(Type root) : Attribute
{
    public Type Root { get; } = root;
}

/// <summary>
/// Code outside an aggregate let in to its internals: its loader, or one of the context's operations on it (a delete it
/// does through the root). Each one named here, so letting code in is a choice a review sees (AggregateBoundaryTests).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class AggregateAccessAttribute(Type root) : Attribute
{
    public Type Root { get; } = root;
}
