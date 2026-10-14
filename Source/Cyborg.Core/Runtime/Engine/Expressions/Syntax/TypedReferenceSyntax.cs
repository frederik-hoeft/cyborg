namespace Cyborg.Core.Runtime.Engine.Expressions.Syntax;

/// <summary>Whole-value lazy typed reference.</summary>
internal sealed record TypedReferenceSyntax(VariableReferenceSyntax Reference) : ValueSyntax;
