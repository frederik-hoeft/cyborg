using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Cyborg.Core.Modules.Runtime.Environments.Syntax;

public sealed partial class VariableSyntaxBuilder(JsonNamingPolicy namingPolicy)
{
    internal JsonNamingPolicy NamingPolicy { get; } = namingPolicy ?? throw new ArgumentNullException(nameof(namingPolicy));

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z_0-9\-\.]*$")]
    internal partial Regex IdentifierRegex { get; }

    [GeneratedRegex(@"^\$\{(?<expression>@@|@(?:[A-Za-z_][A-Za-z_0-9\-\.]*)?|[A-Za-z_][A-Za-z_0-9\-\.]*)\}$")]
    internal partial Regex VariableRegex { get; }

    [GeneratedRegex(@"\$\{(?<expression>@@|@(?:[A-Za-z_][A-Za-z_0-9\-\.]*)?|[A-Za-z_][A-Za-z_0-9\-\.]*)\}")]
    internal partial Regex InterpolationRegex { get; }

    public bool IsValidIdentifier(ReadOnlySpan<char> identifier)
    {
        if (identifier.IsWhiteSpace())
        {
            return false;
        }
        return IdentifierRegex.IsMatch(identifier);
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