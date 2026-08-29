using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Hooks;

namespace Cyborg.Core.Runtime.Services.Debugging;

internal sealed class DebuggingHook(IServiceProvider serviceProvider, IWorkflowDebugger? debugger = null) : IModulePreExecutionHook
{
    // run before most other hooks, so that we can pause before the module executes
    public int Priority => -short.MaxValue;

    public async ValueTask<IModuleExecutionResult<TModule>?> ExecuteAsync<TModule>(TModule module, IModulePreExecutionContext context, CancellationToken cancellationToken)
        where TModule : ModuleBase, IModule<TModule>
    {
        // Debug boundary: after preparation and constraint evaluation, but before validation is enforced for execution.
        // This lets stepping stop on invalid modules and inspect both the prepared module and its validation errors.
        if (debugger is { IsEnabled: true })
        {
            DebugResumeAction resumeAction = await debugger.EvaluatePreExecutionAsync(TModule.ModuleId, context.ValidationResult, context.Runtime, serviceProvider, cancellationToken);
            if (resumeAction is DebugResumeAction.Cancel)
            {
                return context.ResultBuilder.Canceled(module);
            }
        }
        return null;
    }
}
