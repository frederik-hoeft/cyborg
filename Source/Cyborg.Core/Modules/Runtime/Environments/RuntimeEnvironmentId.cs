namespace Cyborg.Core.Modules.Runtime.Environments;

internal readonly record struct RuntimeEnvironmentId(Guid Value)
{
    public static RuntimeEnvironmentId Create() => new(Guid.CreateVersion7());
}
