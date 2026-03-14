using System.Text;
using System.Text.Json;

namespace Cyborg.Core.Modules.Runtime.Environments.Syntax;

public sealed class VariableSyntaxBuilder(JsonNamingPolicy namingPolicy)
{
    internal JsonNamingPolicy NamingPolicy { get; } = namingPolicy ?? throw new ArgumentNullException(nameof(namingPolicy));

    public PathSyntax Path(string? segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            return Root();
        }
        return new PathSyntax(NamingPolicy, VariableSyntaxHelpers.NormalizePath(segment, nameof(segment)).ToString());
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