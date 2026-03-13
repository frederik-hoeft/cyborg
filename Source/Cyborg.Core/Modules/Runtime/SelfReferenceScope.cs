using Cyborg.Core.Modules.Runtime.Environments;

namespace Cyborg.Core.Modules.Runtime;

public readonly struct SelfReferenceScope(IRuntimeEnvironment environment, string? reference) : IDisposable
{
    public void Dispose()
    {
        if (!string.IsNullOrEmpty(reference))
        {
            environment.SetVariable(environment.Self, reference);
        }
    }
}