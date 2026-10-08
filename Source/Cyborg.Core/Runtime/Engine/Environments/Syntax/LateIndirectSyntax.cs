namespace Cyborg.Core.Runtime.Engine.Environments.Syntax;

public readonly record struct LateIndirectSyntax
{
    private string Value { get; }

    internal LateIndirectSyntax(string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        Value = $"&{{@{identifier}}}";
    }

    public override string ToString() => Value;

    public static implicit operator string(LateIndirectSyntax value) => value.ToString();
}
