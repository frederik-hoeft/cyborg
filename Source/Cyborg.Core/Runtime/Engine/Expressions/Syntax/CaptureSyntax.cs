namespace Cyborg.Core.Runtime.Engine.Expressions.Syntax;

/// <summary>Whole-value eager typed capture; its evaluation belongs to the binding phase.</summary>
internal sealed record CaptureSyntax(VariableReferenceSyntax Reference) : ValueSyntax;
