using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Model;
using Microsoft.Extensions.Logging;

namespace Cyborg.Core.Runtime.Engine;

internal sealed class ModuleArtifactPublisher(ILoggerFactory loggerFactory) : IModuleArtifactPublisher
{
    private readonly ILogger _logger = loggerFactory.CreateLogger("cyborg.core.runtime");

    public IModuleExecutionResult Publish<TModule>(IModuleExecutionResult<TModule> result, IModuleRuntime responsibleRuntime, IRuntimeEnvironment currentEnvironment)
        where TModule : ModuleBase, IModuleDefinition
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(responsibleRuntime);
        ArgumentNullException.ThrowIfNull(currentEnvironment);

        IEnvironmentLike artifacts = result.Artifacts.Build(result.Status);
        ModuleEnvironment deploymentTarget = result.Module.Artifacts.Environment;
        IRuntimeEnvironment targetEnvironment = responsibleRuntime.PrepareEnvironment(deploymentTarget);
        _logger.LogArtifactPublishing(currentEnvironment.NamespaceOf(result.Module), TModule.ModuleId, deploymentTarget.Scope.ToString(), targetEnvironment.Name);
        targetEnvironment.Publish(artifacts);
        return new ModuleExecutionResult(result.Module, result.Status, artifacts);
    }
}
