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

    public RefSyntax Child(string segment) =>
        new(NamingPolicy, VariableSyntaxHelpers.Join(Value, VariableSyntaxHelpers.NormalizePath(segment, nameof(segment))).ToString());

    public RefSyntax Child(PathSyntax other) =>
        new(NamingPolicy, VariableSyntaxHelpers.Join(Value, other.ToString()).ToString());

    public RefSyntax Child(RefSyntax other) =>
        new(NamingPolicy, VariableSyntaxHelpers.Join(Value, other.ToString()).ToString());

    public RefSyntax Child(LateRefSyntax other) =>
        new(NamingPolicy, VariableSyntaxHelpers.Join(Value, other.ToString()).ToString());

    public override string ToString() => Value;

    internal static string UncheckedMakeRef(string s) => $"${{{s}}}";

    public static implicit operator string(RefSyntax value) => value.ToString();
}