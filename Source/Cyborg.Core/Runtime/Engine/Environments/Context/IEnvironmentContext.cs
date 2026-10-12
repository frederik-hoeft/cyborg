namespace Cyborg.Core.Runtime.Engine.Environments.Context;

/// <summary>
/// Read-only contextual values owned by one environment view.
/// Contributors answer dotted paths relative to the <c>@</c> self-reference, such as <c>origin.file</c>.
/// </summary>
internal interface IEnvironmentContext
{
    bool TryGetValue(string path, [NotNullWhen(true)] out object? value);
}
