using Cyborg.Core.Modules.Runtime.Environments;

namespace Cyborg.Core.Modules.Extensions;

[SuppressMessage("Design", CA1034, Justification = CA1034_JUSTIFY_EXTENSION_SYNTAX_CSHARP_14)]
public static class VariableResolverScopeExtensions
{
    extension (IVariableResolverScope self)
    {
        [return: NotNullIfNotNull(nameof(defaultValue))]
        public T? ResolveVariableOrDefault<T>(string name, T? defaultValue = default)
        {
            if (self.TryResolveVariable(name, out T? result))
            {
                return result;
            }
            return defaultValue;
        }
    }
}