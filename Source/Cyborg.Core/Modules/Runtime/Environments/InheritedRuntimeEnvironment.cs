using Cyborg.Core.Modules.Runtime.Environments.Syntax;

namespace Cyborg.Core.Modules.Runtime.Environments;

internal sealed record InheritedRuntimeEnvironment(string Name, IRuntimeEnvironment Parent, bool IsTransient, VariableSyntaxBuilder SyntaxFactory, string Namespace)
    : RuntimeEnvironment(Name, IsTransient, SyntaxFactory, Namespace)
{
    internal protected override bool TryResolveVariableRecursiveCore(ResolutionContext context, [NotNullWhen(true)] out object? value)
    {
        if (TryResolveVariableInCurrentScopeCore(context, out value))
        {
            return true;
        }
        if (Parent is EnvironmentLike parent)
        {
            return parent.TryResolveVariableRecursiveCore(context, out value);
        }
        return Parent.TryResolveVariable(context.Name, out value);
    }

    [return: NotNullIfNotNull(nameof(value))]
    internal protected override IReadOnlyCollection<T>? ResolveCollectionCore<TModule, T>(EnvironmentLike entryPoint, TModule module, IReadOnlyCollection<T>? value, string? moduleExpression, string? valueExpression)
    {
        IReadOnlyCollection<T>? resolvedValue = base.ResolveCollectionCore(entryPoint, module, value, moduleExpression, valueExpression);
        if (resolvedValue is not null && !resolvedValue.Equals(value))
        {
            return resolvedValue;
        }
        if (Parent is RuntimeEnvironment runtimeParent)
        {
            return runtimeParent.ResolveCollectionCore(entryPoint, module, value, moduleExpression, valueExpression);
        }
        return Parent.ResolveCollection(module, value, moduleExpression, valueExpression);
    }

    [return: NotNullIfNotNull(nameof(value))]
    internal protected override T? ResolveCore<TModule, T>(EnvironmentLike entryPoint, TModule module, T? value, string? moduleExpression, string? valueExpression) where T : default
    {
        T? resolvedValue = base.ResolveCore(entryPoint, module, value, moduleExpression, valueExpression);
        if (resolvedValue is not null && !resolvedValue.Equals(value))
        {
            return resolvedValue;
        }
        if (Parent is RuntimeEnvironment runtimeParent)
        {
            return runtimeParent.ResolveCore(entryPoint, module, value, moduleExpression, valueExpression);
        }
        return Parent.Resolve(module, value, moduleExpression, valueExpression);
    }
}
