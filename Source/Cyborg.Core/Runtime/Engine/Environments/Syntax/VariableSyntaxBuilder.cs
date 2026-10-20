using Cyborg.Core.Parsing.Parsers;
using Cyborg.Core.Parsing.SyntaxNodes;
using Cyborg.Core.Runtime.Engine.Expressions.Parsers;
using Cyborg.Core.Runtime.Engine.Expressions.Syntax;
using Cyborg.Core.Runtime.Engine.Expressions.SyntaxNodes;
using System.Text;
using System.Text.Json;

namespace Cyborg.Core.Runtime.Engine.Environments.Syntax;

public sealed class VariableSyntaxBuilder(JsonNamingPolicy namingPolicy)
{
    internal JsonNamingPolicy NamingPolicy { get; } = namingPolicy ?? throw new ArgumentNullException(nameof(namingPolicy));

    public bool IsValidIdentifier([NotNullWhen(true)] string? identifier) =>
        identifier is not null && IsValidIdentifier(identifier.AsSpan());

    public bool IsValidIdentifier(ReadOnlySpan<char> identifier)
    {
        if (identifier.IsEmpty || identifier.IsWhiteSpace())
        {
            return false;
        }
        // Identifiers are the unmodified, named subset of the expression language's references.
        // Reusing the same parser keeps validation consistent with indirection and interpolation.
        return VariableReferenceParser.Instance.TryParseComplete(identifier.ToString(), out ISyntaxNode? node)
            && node is VariableReferenceNode reference
            && reference.Evaluate() is { Identifier: not null, Anchor: ReferenceAnchor.CurrentScope } parsed
            && !ContextualVariableSyntax.IsContextualName(parsed.Identifier);
    }

    public bool IsValidNamespace([NotNullWhen(true)] string? ns) =>
        ns is not null && IsValidNamespace(ns.AsSpan());

    public bool IsValidNamespace(ReadOnlySpan<char> ns) => IsValidIdentifier(ns);

    public PathSyntax Path(string? segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            return Root();
        }
        return new PathSyntax(NamingPolicy, VariableSyntaxHelpers.NormalizePath(segment, nameof(segment)).ToString());
    }

    public PathSyntax Path(ReadOnlySpan<char> first, ReadOnlySpan<char> second)
    {
        StringBuilder builder = new();
        VariableSyntaxHelpers.Join(builder, VariableSyntaxHelpers.NormalizePath(first, nameof(first)));
        VariableSyntaxHelpers.Join(builder, VariableSyntaxHelpers.NormalizePath(second, nameof(second)));
        return new PathSyntax(NamingPolicy, builder.ToString());
    }

    public PathSyntax Path(params ReadOnlySpan<string> segments)
    {
        if (segments.Length == 0)
        {
            return Root();
        }
        StringBuilder builder = new();
        foreach (string segment in segments)
        {
            VariableSyntaxHelpers.Join(builder, VariableSyntaxHelpers.NormalizePath(segment, nameof(segments)));
        }
        return new PathSyntax(NamingPolicy, builder.ToString());
    }

    public PathSyntax Root() => new(NamingPolicy, string.Empty);

    public SelfSyntax Self() => new(NamingPolicy);

    internal string ConvertMember(string memberName)
    {
        ArgumentException.ThrowIfNullOrEmpty(memberName);
        return NamingPolicy.ConvertName(memberName);
    }
}
