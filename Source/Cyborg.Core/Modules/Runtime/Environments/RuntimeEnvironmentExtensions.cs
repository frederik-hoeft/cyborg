using Cyborg.Core.Modules.Configuration.Model;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Modules.Runtime.Environments;

[SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "False positive for C# 14 extension syntax.")]
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