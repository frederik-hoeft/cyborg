namespace Cyborg.Core.Modules.Runtime.Environments;

internal sealed record RuntimeEnvironmentSeed(
    RuntimeEnvironmentId EnvironmentId,
    RuntimeEnvironmentNode Node,
    IReadOnlyCollection<KeyValuePair<string, object?>> Values,
    bool RegisterName);

internal sealed record RuntimeEnvironmentTransactionSeed(
    RuntimeEnvironmentId GlobalEnvironmentId,
    IReadOnlyCollection<RuntimeEnvironmentSeed> Environments);
