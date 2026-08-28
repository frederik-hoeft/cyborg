namespace Cyborg.Core.Runtime.Engine.Environments;

internal readonly record struct RuntimeEnvironmentId(Guid Value)
{
    public static RuntimeEnvironmentId Create() => new(Guid.CreateVersion7());
}
