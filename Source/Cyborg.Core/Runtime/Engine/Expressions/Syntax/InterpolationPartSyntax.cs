namespace Cyborg.Core.Runtime.Engine.Expressions.Syntax;

/// <summary>Lazy textual reference resolved at evaluation time.</summary>
internal sealed record InterpolationPartSyntax(VariableReferenceSyntax Reference) : TextPartSyntax;
