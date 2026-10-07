namespace Cyborg.Core.Runtime.Engine.Environments;

/// <summary>
/// Stores an eager-capture snapshot so later reads return the same reference without evaluating it again.
/// </summary>
internal sealed class CapturedValue(object? value)
{
    public object? Value { get; } = value;
}
