using Cyborg.Core.Runtime.Engine.Environments;

namespace Cyborg.Core.Runtime.Extensions;

[SuppressMessage("Design", CA1034, Justification = CA1034_JUSTIFY_EXTENSION_SYNTAX_CSHARP_14)]
public static class VariableResolverScopeExtensions
{
    extension(IVariableResolverScope self)
    {
        /// <summary>Returns the resolved value, including null when defined, or the default only when the variable is absent.</summary>
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
