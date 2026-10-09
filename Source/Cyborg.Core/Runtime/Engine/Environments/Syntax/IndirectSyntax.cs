namespace Cyborg.Core.Runtime.Engine.Environments.Syntax;

public readonly record struct IndirectSyntax
{
    private string Value { get; }

    internal IndirectSyntax(string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        Value = $"&{{{identifier}}}";
    }

    public override string ToString() => Value;

    public static implicit operator string(IndirectSyntax value) => value.ToString();
}
