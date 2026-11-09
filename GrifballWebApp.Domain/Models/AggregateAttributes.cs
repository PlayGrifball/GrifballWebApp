namespace GrifballWebApp.Domain.Models;

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
/// Code of the Domain outside an aggregate let in to its internals: another aggregate it works with, or a configuration that
/// seeds it. Each one named here, so letting code in is a choice a review sees (AggregateBoundaryTests). Code outside the
/// Domain (the context included) can't be let in: the compiler keeps it to the public members.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class AggregateAccessAttribute(Type root) : Attribute
{
    public Type Root { get; } = root;
}

/// <summary>
/// The root of an aggregate: made only by its factories, changed only by its methods. It and its parts have no public
/// constructor and no public setter (AggregateBoundaryTests).
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AggregateRootAttribute : Attribute;
