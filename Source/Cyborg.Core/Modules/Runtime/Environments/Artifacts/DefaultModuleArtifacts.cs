using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime.Environments.Syntax;

namespace Cyborg.Core.Modules.Runtime.Environments.Artifacts;

internal sealed class DefaultModuleArtifacts<TModule>(TModule module, IEnvironmentLike artifacts) : IModuleArtifactsBuilder where TModule : ModuleBase, IModule
{
    public VariableSyntaxBuilder SyntaxFactory => artifacts.SyntaxFactory;

    public string Namespace => artifacts.Namespace;

    public IModuleArtifactsBuilder Expose(string path, object? artifact)
    {
        artifacts.SetVariable(SyntaxFactory.Path(path), artifact);
        return this;
    }

    public IModuleArtifactsBuilder Expose(string ns, string name, object? artifact)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ns);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        artifacts.SetVariable(SyntaxFactory.Path(ns).Child(name), artifact);
        return this;
    }

    IModuleArtifactsBuilder IModuleArtifactsBuilder.Expose<T>(T artifact)
    {
        artifacts.Publish(Namespace, artifact, module.Artifacts.DecompositionStrategy, module.Artifacts.PublishNullValues);
        return this;
    }

    IModuleArtifactsBuilder IModuleArtifactsBuilder.Expose<T>(string path, T artifact)
    {
        artifacts.Publish(path, artifact, module.Artifacts.DecompositionStrategy, module.Artifacts.PublishNullValues);
        return this;
    }

    IEnvironmentLike IModuleArtifactsBuilder.Build(ModuleExitStatus exitStatus)
    {
        PathSyntax exitStatusName = SyntaxFactory.Path(Namespace, module.Artifacts.ExitStatusName);
        artifacts.SetVariable(exitStatusName, exitStatus);
        return artifacts;
    }
}