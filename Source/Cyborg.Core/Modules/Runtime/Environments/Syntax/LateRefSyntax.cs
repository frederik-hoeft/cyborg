using System.Text.Json;

namespace Cyborg.Core.Modules.Runtime.Environments.Syntax;

public readonly record struct LateRefSyntax : IChildSyntaxProvider<LateRefSyntax>
{
    private JsonNamingPolicy NamingPolicy { get; }

    private string Value { get; }

    internal LateRefSyntax(JsonNamingPolicy namingPolicy, string value)
    {
        NamingPolicy = namingPolicy ?? throw new ArgumentNullException(nameof(namingPolicy));
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    public bool IsEmpty => string.IsNullOrEmpty(Value);

    JsonNamingPolicy IChildSyntaxProvider<LateRefSyntax>.NamingPolicy => NamingPolicy;

    public LateRefSyntax Child(string segment) =>
        new(NamingPolicy, VariableSyntaxHelpers.Join(Value, VariableSyntaxHelpers.NormalizePath(segment, nameof(segment))).ToString());

    public LateRefSyntax Child(PathSyntax other) =>
        new(NamingPolicy, VariableSyntaxHelpers.Join(Value, other.ToString()).ToString());

    public RefSyntax Child(RefSyntax other) =>
        new(NamingPolicy, VariableSyntaxHelpers.Join(Value, other.ToString()).ToString());

    public RefSyntax Child(LateRefSyntax other) =>
        new(NamingPolicy, VariableSyntaxHelpers.Join(Value, other.ToString()).ToString());

    public override string ToString() => Value;

    public static implicit operator string(LateRefSyntax value) => value.ToString();
}
