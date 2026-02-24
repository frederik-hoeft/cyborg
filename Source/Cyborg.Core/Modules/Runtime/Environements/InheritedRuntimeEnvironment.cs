using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Modules.Runtime.Environements;

internal sealed class InheritedRuntimeEnvironment(string name, IRuntimeEnvironment parent, bool transient) : RuntimeEnvironment(name, transient)
{
    public override bool TryResolveVariable<T>(string name, [NotNullWhen(true)] out T? value) where T : default
    {
        if (base.TryResolveVariable<T>(name, out value))
        {
            return true;
        }
        return parent.TryResolveVariable(name, out value);
    }
}