using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Modules.Logging;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Sequence;

// sample sequence module worker that executes each step in order and returns false if any step fails
public sealed class SequenceModuleWorker(IWorkerContext<SequenceModule> context, ILoggerFactory loggerFactory) : ModuleWorker<SequenceModule>(context)
{
    private readonly ILogger<SequenceModuleWorker> _logger = loggerFactory.CreateLogger<SequenceModuleWorker>();

    protected async override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        ModuleExitStatus status = ModuleExitStatus.Skipped;
        int stepIndex = 0;
        int totalSteps = Module.Steps.Count;
        foreach (ModuleContext step in Module.Steps)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogModuleCanceled(ModuleId);
                return runtime.Exit(Canceled());
            }
            _logger.LogSequenceStep(++stepIndex, totalSteps);
            IModuleExecutionResult result = await runtime.ExecuteAsync(step, cancellationToken);
            if (result.Status is ModuleExitStatus.Canceled or ModuleExitStatus.Failed)
            {
                _logger.LogModuleCanceled(ModuleId);
                return runtime.Exit(WithStatus(result.Status));
            }
            if (result.Status is ModuleExitStatus.Success)
            {
                status = ModuleExitStatus.Success;
            }
        }
        _logger.LogSequenceCompleted(status.ToString());
        return runtime.Exit(WithStatus(status));
    }
}
