namespace Cyborg.Core.Runtime.Engine;

/// <summary>Stable identity of one logical module invocation.</summary>
public readonly record struct ModuleExecutionId(Guid Value)
{
    internal static ModuleExecutionId Create() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
