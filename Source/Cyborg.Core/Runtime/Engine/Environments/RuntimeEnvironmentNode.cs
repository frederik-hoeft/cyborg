namespace Cyborg.Core.Runtime.Engine.Environments;

internal sealed record RuntimeEnvironmentParent(
    RuntimeEnvironmentId EnvironmentId,
    string Namespace,
    IReadOnlyCollection<string> OverrideResolutionTags);

internal sealed record RuntimeEnvironmentNode(
    string Name,
    bool IsTransient,
    RuntimeEnvironmentParent? Parent);
