using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Cyborg.Core.Modules.Runtime.Environments.Syntax;

public sealed partial class VariableSyntaxBuilder(JsonNamingPolicy namingPolicy)
{
    internal JsonNamingPolicy NamingPolicy { get; } = namingPolicy ?? throw new ArgumentNullException(nameof(namingPolicy));

    [GeneratedRegex(VariableGrammar.IDENTIFIER_PATTERN)]
    internal partial Regex IdentifierRegex { get; }

    [GeneratedRegex(VariableGrammar.INDIRECTION_PATTERN)]
    internal partial Regex IndirectionRegex { get; }

    [GeneratedRegex(VariableGrammar.INTERPOLATION_PATTERN)]
    internal partial Regex InterpolationRegex { get; }

    [GeneratedRegex(VariableGrammar.HASH_LITERAL_PATTERN)]
    internal partial Regex HashLiteralRegex { get; }

    [GeneratedRegex(VariableGrammar.NAMESPACE_PATTERN)]
    internal partial Regex NamespaceRegex { get; }

    public bool IsValidIdentifier([NotNullWhen(true)] string? identifier) =>
        identifier is not null && IsValidIdentifier(identifier.AsSpan());

    public bool IsValidIdentifier(ReadOnlySpan<char> identifier)
    {
        if (identifier.IsWhiteSpace())
        {
            return false;
        }
        return IdentifierRegex.IsMatch(identifier);
    }

    public bool IsValidNamespace([NotNullWhen(true)] string? ns) =>
        ns is not null && IsValidNamespace(ns.AsSpan());

    public bool IsValidNamespace(ReadOnlySpan<char> ns)
    {
        if (ns.IsWhiteSpace())
        {
            return false;
        }
        return NamespaceRegex.IsMatch(ns);
    }

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
