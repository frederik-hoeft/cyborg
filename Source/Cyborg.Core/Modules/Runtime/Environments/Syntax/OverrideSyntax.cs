using System.Text;
using System.Text.Json;

namespace Cyborg.Core.Modules.Runtime.Environments.Syntax;

public readonly record struct OverrideSyntax : IChildSyntaxProvider<OverrideSyntax>
{
    private JsonNamingPolicy NamingPolicy { get; }

    private StringBuilder Builder { get; }

    JsonNamingPolicy IChildSyntaxProvider<OverrideSyntax>.NamingPolicy => NamingPolicy;

    internal OverrideSyntax(JsonNamingPolicy namingPolicy, string value)
    {
        ArgumentNullException.ThrowIfNull(namingPolicy);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        NamingPolicy = namingPolicy;
        Builder = new StringBuilder("@").Append(value);
    }

    public OverrideSyntax Child(string segment)
    {
        VariableSyntaxHelpers.Join(Builder, segment);
        return this;
    }

    public OverrideSyntax Child(PathSyntax other)
    {
        VariableSyntaxHelpers.Join(Builder, other.ToString());
        return this;
    }

    public override string ToString() => Builder.ToString();

    public static implicit operator string(OverrideSyntax value) => value.ToString();
}