namespace Cyborg.Core.Modules.Runtime.Environments;

internal sealed record EnvironmentVariableStoreSeed(
    RuntimeEnvironmentId EnvironmentId,
    IReadOnlyCollection<KeyValuePair<string, object?>> Values);
