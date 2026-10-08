namespace Cyborg.Core.Runtime.Engine.Environments.Syntax;

public readonly record struct CaptureSyntax
{
    private string Value { get; }

    internal CaptureSyntax(string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        Value = $"*{{{identifier}}}";
    }

    public override string ToString() => Value;

    public static implicit operator string(CaptureSyntax value) => value.ToString();
}
