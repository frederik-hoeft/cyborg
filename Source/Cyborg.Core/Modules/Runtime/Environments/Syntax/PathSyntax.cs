using System.Text.Json;

namespace Cyborg.Core.Modules.Runtime.Environments.Syntax;

public readonly record struct PathSyntax : IChildSyntaxProvider<PathSyntax>
{
    private JsonNamingPolicy NamingPolicy { get; }

    private string Value { get; }

    internal PathSyntax(JsonNamingPolicy namingPolicy, string value)
    {
        NamingPolicy = namingPolicy ?? throw new ArgumentNullException(nameof(namingPolicy));
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    public bool IsEmpty => string.IsNullOrEmpty(Value);

    JsonNamingPolicy IChildSyntaxProvider<PathSyntax>.NamingPolicy => NamingPolicy;

    public PathSyntax Child(string segment) =>
        new(NamingPolicy, VariableSyntaxHelpers.Join(Value, VariableSyntaxHelpers.NormalizePath(segment, nameof(segment))).ToString());

    public PathSyntax Child(PathSyntax other) =>
        new(NamingPolicy, VariableSyntaxHelpers.Join(Value, other.Value).ToString());

    public PathSyntax Child(RefSyntax other) =>
        new(NamingPolicy, VariableSyntaxHelpers.Join(Value, other.ToString()).ToString());

    public OverrideSyntax Override()
    {
        VariableSyntaxHelpers.ThrowIfEmpty(Value, "Cannot create an override from an empty path.");
        return new OverrideSyntax(NamingPolicy, Value);
    }

    public RefSyntax Ref()
    {
        VariableSyntaxHelpers.ThrowIfEmpty(Value, "Cannot create a reference from an empty path.");
        return new RefSyntax(NamingPolicy, $"${{{Value}}}");
    }

    public override string ToString() => Value;

    public static implicit operator string(PathSyntax value) => value.ToString();
}