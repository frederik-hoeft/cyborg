namespace Cyborg.Core.Runtime.Engine.Environments;

/// <summary>
/// Captures the structural parent's logical identity and view bindings at environment creation time.
/// Reconstructing the parent must use these bindings, independently of active module invocation frames.
/// </summary>
internal sealed record RuntimeEnvironmentParent(
    RuntimeEnvironmentId EnvironmentId,
    string Namespace,
    IReadOnlyCollection<string> OverrideResolutionTags);

internal sealed record RuntimeEnvironmentNode(
    string Name,
    bool IsTransient,
    RuntimeEnvironmentParent? Parent);
