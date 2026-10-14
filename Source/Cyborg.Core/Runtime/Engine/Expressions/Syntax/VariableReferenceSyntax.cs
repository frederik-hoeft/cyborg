namespace Cyborg.Core.Runtime.Engine.Expressions.Syntax;

/// <summary>
/// Variable reference. A null identifier denotes the current namespace, whose scope is selected by <see cref="Anchor"/>.
/// </summary>
internal sealed record VariableReferenceSyntax(string? Identifier, ReferenceAnchor Anchor);
