namespace Cyborg.Core.Runtime.Engine.Expressions.Syntax;

/// <summary>Variable reference. A null identifier denotes the namespace of the selected scope.</summary>
internal sealed record VariableReferenceSyntax(string? Identifier, ReferenceAnchor Anchor)
{
    public override string ToString() => Identifier is null
        ? Anchor == ReferenceAnchor.EntryPoint ? "@@" : "@"
        : Anchor == ReferenceAnchor.EntryPoint ? $"@{Identifier}" : Identifier;
}
