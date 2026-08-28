using Cyborg.Core.Runtime.Model;

namespace Cyborg.Core.Runtime.Engine.Environments;

[SuppressMessage("Design", CA1034, Justification = CA1034_JUSTIFY_EXTENSION_SYNTAX_CSHARP_14)]
public static class RuntimeEnvironmentExtensions
{
    extension(IRuntimeEnvironment environment)
    {
        public string NamespaceOf<TModule>(TModule module) where TModule : IModuleDefinition
        {
            ArgumentNullException.ThrowIfNull(module);
            return GetEffectiveNamespace(module.Name, module.Group, TModule.ModuleId);
        }

        public string NamespaceOf(ModuleReference moduleReference)
        {
            ArgumentNullException.ThrowIfNull(moduleReference);
            return GetEffectiveNamespace(moduleReference.Definition.Name, moduleReference.Definition.Group, moduleReference.ModuleId);
        }

        public string NamespaceOf(ModuleContext moduleContext)
        {
            ArgumentNullException.ThrowIfNull(moduleContext);
            return environment.NamespaceOf(moduleContext.Module);
        }

        internal string NamespaceOf(IModuleWorker module)
        {
            ArgumentNullException.ThrowIfNull(module);
            return GetEffectiveNamespace(module.Module.Name, module.Module.Group, module.ModuleId);
        }

        internal IRuntimeEnvironment Bind(IModuleWorker module)
        {
            ArgumentNullException.ThrowIfNull(module);
            return environment.Bind(environment.NamespaceOf(module));
        }

        public void Publish(IEnvironmentLike other)
        {
            ArgumentNullException.ThrowIfNull(other);
            foreach ((string key, object? value) in other)
            {
                environment.SetVariable(key, value);
            }
        }
    }

    private static string GetEffectiveNamespace(string? name, string? group, string moduleId) => (name, group) switch
    {
        ({ Length: > 0 }, _) => name,
        (_, { Length: > 0 }) => group,
        _ => moduleId
    };
}
