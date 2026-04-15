using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Modules.Runtime.Environments.Artifacts;
using Cyborg.Core.Modules.Runtime.Environments.Syntax;
using Cyborg.Core.Modules.Validation;

namespace Cyborg.Modules.Conditions;

public abstract class ConditionalModuleWorkerBase<TModule>(IWorkerContext<TModule> context)
    : ModuleWorker<TModule>(context) where TModule : ModuleBase, IModule<TModule>
{
    protected IRuntimeEnvironment CreateChildEnvironment(IModuleRuntime runtime, ModuleReference child, PathSyntax childNamespace)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(child);

        ModuleArtifacts childArtifacts = ModuleArtifacts.Default with
        {
            Namespace = childNamespace,
            DecompositionStrategy = DecompositionStrategy.LeavesOnly,
            Environment = ArtifactModuleEnvironment.Default with { Scope = EnvironmentScope.Parent } // need artifacts to be accessible to us
        };
        IRuntimeEnvironment environment = runtime.PrepareEnvironment(ModuleEnvironment.Default);
        // @<child_module_id>.artifacts via @override of child property
        string artifactsOverride = environment.SyntaxFactory.Path(environment.NamespaceOf(child)).Property(Module.Artifacts).Override();
        environment.SetVariable(artifactsOverride, childArtifacts);
        return environment;
    }
}