using Cyborg.Core.Runtime;
using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Engine.Environments.Artifacts;
using Cyborg.Core.Runtime.Engine.Environments.Syntax;
using Cyborg.Core.Runtime.Model;

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
