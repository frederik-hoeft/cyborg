using System.Text.Json;

namespace Cyborg.Core.Modules.Runtime.Environments.Syntax;

public readonly record struct RefSyntax : IChildSyntaxProvider<RefSyntax>
{
    private JsonNamingPolicy NamingPolicy { get; }

    private string Value { get; }

    JsonNamingPolicy IChildSyntaxProvider<RefSyntax>.NamingPolicy => NamingPolicy;

    internal RefSyntax(JsonNamingPolicy namingPolicy, string value)
    {
        ArgumentNullException.ThrowIfNull(namingPolicy);
        ArgumentNullException.ThrowIfNull(value);
        NamingPolicy = namingPolicy;
        Value = UncheckedMakeRef(value);
    }

    private RefSyntax(RefSyntax other, ReadOnlySpan<char> segment)
    {
        ArgumentNullException.ThrowIfNull(other.NamingPolicy);

        NamingPolicy = other.NamingPolicy;
        Value = VariableSyntaxHelpers.Join(other.Value, segment).ToString();
    }

    public RefSyntax Child(string segment) => new(this, VariableSyntaxHelpers.NormalizePath(segment, nameof(segment)));

    public RefSyntax Child(PathSyntax other) => new(this, other.ToString());

    public RefSyntax Child(RefSyntax other) => new(this, other.ToString());

    public RefSyntax Child(LateRefSyntax other) => new(this, other.ToString());

    public override string ToString() => Value;

    internal static string UncheckedMakeRef(string s) => $"${{{s}}}";

    public static implicit operator string(RefSyntax value) => value.ToString();
}
