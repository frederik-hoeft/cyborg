using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Modules.Shared.Model;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Shared.Extensions;

[SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "False positive for C# 14 extension classes.")]
public static class ModuleRuntimeExtensions
{
    extension (IModuleRuntime self)
    {
        [return: NotNullIfNotNull(nameof(defaultValue))]
        public IRuntimeEnvironment? ResolveEnvironmentReference(ModuleEnvironmentReference? environmentReference, IRuntimeEnvironment? defaultValue = null) => environmentReference switch
        {
            (EnvironmentScopeReference.Current, _) => self.Environment,
            (EnvironmentScopeReference.Global, _) => self.GlobalEnvironment,
            (EnvironmentScopeReference.Parent, _) => self.ParentEnvironment,
            (EnvironmentScopeReference.Reference, { Length: > 0 } name) when self.TryGetEnvironment(name, out IRuntimeEnvironment? env) => env,
            _ => defaultValue
        };
    }
}
