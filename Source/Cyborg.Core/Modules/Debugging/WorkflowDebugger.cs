using Cyborg.Core.Modules.Descriptors;
using Cyborg.Core.Modules.Runtime;
using Microsoft.Extensions.Logging;

namespace Cyborg.Core.Modules.Debugging;

public sealed class WorkflowDebugger(
    IBreakpointRegistry breakpoints,
    IModuleDescriptionSerializerRegistry descriptionSerializers,
    ILoggerFactory loggerFactory) : IWorkflowDebugger
{
    /// <summary>
    /// Expression used to implement <c>step</c> via the shared breakpoint matcher.
    /// </summary>
    public const string STEP_EXPRESSION = ".*";

    private readonly IModuleDescriptionSerializer _textSerializer =
        (descriptionSerializers ?? throw new ArgumentNullException(nameof(descriptionSerializers)))
        .GetRequired(ModuleDescriptionFormats.TEXT);
    private readonly ILogger _logger =
        (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory)))
        .CreateLogger("cyborg.core.debugging");

    public IBreakpointRegistry Breakpoints { get; } =
        breakpoints ?? throw new ArgumentNullException(nameof(breakpoints));

    public IDebugFrontend? Frontend { get; set; }

    public bool IsEnabled => Breakpoints.Count > 0;

    public async ValueTask<DebugResumeAction> EvaluatePreExecutionAsync(
        IModule module,
        string moduleId,
        IModuleRuntime runtime,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentNullException.ThrowIfNull(runtime);

        if (!IsEnabled)
        {
            return DebugResumeAction.Continue;
        }

        // CONSIDER: pass match context (id, name, group) as a composite object to avoid parameter creep as more match criteria are added in the future.
        if (!Breakpoints.TryMatchAndConsume(
            moduleId,
            module.Name,
            module.Group,
            out BreakpointExpression? matched))
        {
            return DebugResumeAction.Continue;
        }

        cancellationToken.ThrowIfCancellationRequested();

        DebugPauseContext pauseContext = new(
            module,
            moduleId,
            runtime,
            Breakpoints,
            _textSerializer,
            requestStep: () => Breakpoints.Add(STEP_EXPRESSION, isOneShot: true),
            detach: Breakpoints.Clear);

        // TODO: use ZLogger
        _logger.LogDebug(
            "Breakpoint hit for module '{ModuleIdentity}' (expression {Expression})",
            pauseContext.ModuleIdentity,
            matched!.Expression);

        if (Frontend is null)
        {
            // No interactive adapter: treat as a soft break and continue.
            return DebugResumeAction.Continue;
        }

        return await Frontend.PauseAsync(pauseContext, cancellationToken).ConfigureAwait(false);
    }
}
