using Cyborg.Core.Modules.Runtime.Environments.Syntax;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Cyborg.Core.Modules.Runtime.Environments;

internal sealed record InheritedRuntimeEnvironment(string Name, IRuntimeEnvironment Parent, bool IsTransient, VariableSyntaxBuilder SyntaxFactory, string Namespace) 
    : RuntimeEnvironment(Name, IsTransient, SyntaxFactory, Namespace)
{
    public override bool TryResolveVariable<T>(string name, [NotNullWhen(true)] out T? value) where T : default
    {
        if (base.TryResolveVariable(name, out value))
        {
            return true;
        }
        return Parent.TryResolveVariable(name, out value);
    }

    [return: NotNullIfNotNull(nameof(value))]
    public override T? Resolve<TModule, T>(TModule module, T? value, [CallerArgumentExpression(nameof(module))] string? moduleExpression = null, [CallerArgumentExpression(nameof(value))] string? valueExpression = null) where T : default
    {
        T? v = base.Resolve(module, value, moduleExpression, valueExpression);
        if (v is not null && !v.Equals(value))
        {
            return v;
        }
        return Parent.Resolve(module, value, moduleExpression, valueExpression);
    }
}