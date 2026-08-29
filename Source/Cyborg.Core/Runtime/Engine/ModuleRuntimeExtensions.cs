using Cyborg.Core.Runtime.Configuration;
using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Model;

namespace Cyborg.Core.Runtime.Engine;

[SuppressMessage("Design", CA1034, Justification = CA1034_JUSTIFY_EXTENSION_SYNTAX_CSHARP_14)]
public static class ModuleRuntimeExtensions
{
    extension(IModuleRuntime runtime)
    {
        public Task<IModuleExecutionResult> ExecuteAsync(ModuleContext moduleContext, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(runtime);
            ArgumentNullException.ThrowIfNull(moduleContext);
            IRuntimeEnvironment environment = runtime.PrepareEnvironment(moduleContext.Environment ?? ModuleEnvironment.Default);
            return runtime.ExecuteAsync(moduleContext, environment, cancellationToken);
        }

        public Task<IModuleExecutionResult> ExecuteAsync(ModuleConfigurationLoadResult configuration, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(runtime);
            ArgumentNullException.ThrowIfNull(configuration);
            IRuntimeEnvironment environment = runtime.PrepareEnvironment(configuration.ModuleContext.Environment ?? ModuleEnvironment.Default);
            return runtime.ExecuteAsync(configuration, environment, cancellationToken);
        }

        public Task<IModuleExecutionResult> ExecuteAsync(ModuleReference moduleReference, EnvironmentScope scope = EnvironmentScope.Global, string? name = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(runtime);
            ArgumentNullException.ThrowIfNull(moduleReference);
            ModuleEnvironment moduleEnvironment = new()
            {
                Scope = scope,
                Name = name
            };
            IRuntimeEnvironment environment = runtime.PrepareEnvironment(moduleEnvironment);
            return runtime.ExecuteAsync(moduleReference, environment, cancellationToken);
        }

        public IRuntimeEnvironment PrepareEnvironment(ModuleContext moduleContext, IReadOnlyCollection<string>? overrideResolutionTags = null)
        {
            ArgumentNullException.ThrowIfNull(runtime);
            ArgumentNullException.ThrowIfNull(moduleContext);
            return runtime.PrepareEnvironment(moduleContext.Environment ?? ModuleEnvironment.Default, overrideResolutionTags);
        }

        public IRuntimeEnvironment PrepareEnvironment(ModuleConfigurationLoadResult configuration, IReadOnlyCollection<string>? overrideResolutionTags = null)
        {
            ArgumentNullException.ThrowIfNull(runtime);
            ArgumentNullException.ThrowIfNull(configuration);
            return runtime.PrepareEnvironment(configuration.ModuleContext, overrideResolutionTags);
        }
    }
}
