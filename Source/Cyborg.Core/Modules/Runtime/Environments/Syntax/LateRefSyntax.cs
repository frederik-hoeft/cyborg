using System.Text.Json;

namespace Cyborg.Core.Modules.Runtime.Environments.Syntax;

public readonly record struct LateRefSyntax : IChildSyntaxProvider<LateRefSyntax>
{
    private JsonNamingPolicy NamingPolicy { get; }

    private string Value { get; }

    internal LateRefSyntax(JsonNamingPolicy namingPolicy, string value)
    {
        ArgumentNullException.ThrowIfNull(namingPolicy);
        ArgumentNullException.ThrowIfNull(value);
        NamingPolicy = namingPolicy;
        Value = RefSyntax.UncheckedMakeRef(UncheckedMakeLate(value));
    }

    private LateRefSyntax(LateRefSyntax other, ReadOnlySpan<char> segment)
    {
        ArgumentNullException.ThrowIfNull(other.NamingPolicy);

        NamingPolicy = other.NamingPolicy;
        Value = VariableSyntaxHelpers.Join(other.Value, segment).ToString();
    }

    JsonNamingPolicy IChildSyntaxProvider<LateRefSyntax>.NamingPolicy => NamingPolicy;

    public LateRefSyntax Child(string segment) => new(this, VariableSyntaxHelpers.NormalizePath(segment, nameof(segment)));

    public LateRefSyntax Child(PathSyntax other) => new(this, other.ToString());

    public LateRefSyntax Child(RefSyntax other) => new(this, other.ToString());

    public LateRefSyntax Child(LateRefSyntax other) => new(this, other.ToString());

    public override string ToString() => Value;

    internal static string Symbol => "@";

    internal static string UncheckedMakeLate(string value) => $"{Symbol}{value}";

    internal static string UncheckedMakeLateRef(string value) => RefSyntax.UncheckedMakeRef(UncheckedMakeLate(value));

    public static implicit operator string(LateRefSyntax value) => value.ToString();
}