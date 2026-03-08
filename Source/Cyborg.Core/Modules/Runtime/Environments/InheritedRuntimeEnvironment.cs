using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Cyborg.Core.Modules.Runtime.Environments;

internal sealed class InheritedRuntimeEnvironment(string name, IRuntimeEnvironment parent, bool transient, JsonNamingPolicy namingPolicy) 
    : RuntimeEnvironment(name, transient, namingPolicy)
{
    public override bool TryResolveVariable<T>(string name, [NotNullWhen(true)] out T? value) where T : default
    {
        if (base.TryResolveVariable(name, out value))
        {
            return true;
        }
        return parent.TryResolveVariable(name, out value);
    }

    [return: NotNullIfNotNull(nameof(value))]
    protected override T? Resolve<TModule, T>(TModule module, T? value, [CallerArgumentExpression(nameof(module))] string? moduleExpression = null, [CallerArgumentExpression(nameof(value))] string? valueExpression = null) where T : default
    {
        T? v = base.Resolve(module, value, moduleExpression, valueExpression);
        if (v is not null && !v.Equals(value))
        {
            return v;
        }
        return parent.Resolve(module, value, moduleExpression, valueExpression);
    }
}