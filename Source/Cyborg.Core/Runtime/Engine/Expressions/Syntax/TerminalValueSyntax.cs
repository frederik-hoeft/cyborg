namespace Cyborg.Core.Runtime.Engine.Expressions.Syntax;

/// <summary>
/// Describes a non-text CLR value. The syntax wrapper is immutable; the contained object is not deep-cloned.
/// </summary>
internal sealed record TerminalValueSyntax(object? Value) : ValueSyntax;
