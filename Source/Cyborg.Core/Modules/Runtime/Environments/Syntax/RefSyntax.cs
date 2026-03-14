using System.Text.Json;

namespace Cyborg.Core.Modules.Runtime.Environments.Syntax;

public readonly record struct RefSyntax : IChildSyntaxProvider<RefSyntax>
{
    private JsonNamingPolicy NamingPolicy { get; }

    private string Value { get; }

    JsonNamingPolicy IChildSyntaxProvider<RefSyntax>.NamingPolicy => NamingPolicy;

    internal RefSyntax(JsonNamingPolicy namingPolicy, string value)
    {
        NamingPolicy = namingPolicy ?? throw new ArgumentNullException(nameof(namingPolicy));
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    public RefSyntax Child(string segment) =>
        new(NamingPolicy, VariableSyntaxHelpers.Join(Value, VariableSyntaxHelpers.NormalizePath(segment, nameof(segment))).ToString());

    public RefSyntax Child(PathSyntax other) =>
        new(NamingPolicy, VariableSyntaxHelpers.Join(Value, other.ToString()).ToString());

    public override string ToString() => Value;

    public static implicit operator string(RefSyntax value) => value.ToString();
}