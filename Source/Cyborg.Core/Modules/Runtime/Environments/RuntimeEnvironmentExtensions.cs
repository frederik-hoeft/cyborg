using Cyborg.Core.Modules.Configuration.Model;

namespace Cyborg.Core.Modules.Runtime.Environments;

[SuppressMessage("Design", CA1034, Justification = CA1034_JUSTIFY_EXTENSION_SYNTAX_CSHARP_14)]
public static class RuntimeEnvironmentExtensions
{
    extension (IRuntimeEnvironment environment)
    {
        public string NamespaceOf(ModuleReference moduleReference)
        {
            ArgumentNullException.ThrowIfNull(moduleReference);
            return environment.NamespaceOf(moduleReference.Module);
        }

        public string NamespaceOf(ModuleContext moduleContext)
        {
            ArgumentNullException.ThrowIfNull(moduleContext);
            return environment.NamespaceOf(moduleContext.Module.Module);
        }
    }
}