using Cyborg.Core.Modules.Runtime.Environments.Syntax;
using Cyborg.Core.Text;

namespace Cyborg.Core.Modules.Runtime.Environments.Artifacts;

public sealed class DefaultModuleArtifactsFactory : IModuleArtifactsFactory
{
    private readonly IRuntimeEnvironmentFactory _environmentFactory;

    public DefaultModuleArtifactsFactory(
        VariableSyntaxBuilder syntaxFactory,
        ITaggedStringConversionObserver taggedStringConversionObserver)
        : this(new DefaultRuntimeEnvironmentFactory(syntaxFactory, taggedStringConversionObserver))
    {
    }

    internal DefaultModuleArtifactsFactory(IRuntimeEnvironmentFactory environmentFactory)
    {
        ArgumentNullException.ThrowIfNull(environmentFactory);
        _environmentFactory = environmentFactory;
    }

    public IModuleArtifactsBuilder CreateArtifacts<TModule>(IModuleRuntime runtime, TModule module) where TModule : ModuleBase, IModule
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(module);
        string artifactNamespace = module.Artifacts.Namespace ?? runtime.Environment.Namespace;
        IEnvironmentLike artifacts = _environmentFactory.CreateEnvironmentLike(artifactNamespace);
        return new DefaultModuleArtifacts<TModule>(module, artifacts);
    }
}
