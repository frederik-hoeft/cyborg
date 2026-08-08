using Cyborg.Core.Configuration.Serialization.Dynamics;
using Cyborg.Core.Modules.Debugging.Breakpoints;
using Cyborg.Core.Modules.Debugging.Configuration;
using Cyborg.Core.Services.Default;
using Jab;
using Microsoft.Extensions.Logging;

namespace Cyborg.Core.Modules.Debugging;

[ServiceProviderModule]
[Singleton<IDynamicValueProvider>(Factory = nameof(CreateDebugOptionsProvider))]
[Singleton<IBreakpointRegistry>(Factory = nameof(CreateBreakpointRegistry))]
[Singleton<IWorkflowDebugger>(Factory = nameof(CreateWorkflowDebugger))]
[Singleton<IServiceSelectionKey<IDebugFrontend>>(Instance = nameof(DebugFrontendSelectionKey))]
[Singleton<IDefault<IDebugFrontend>, Default<IDebugFrontend>>]
public interface IDebugServices
{
    static ServiceSelectionKey<IDebugFrontend> DebugFrontendSelectionKey => new("cyborg.core.debug:frontend", DebugOptions.Default.Frontend);

    static IDynamicValueProvider CreateDebugOptionsProvider() => new DebugOptionsProvider();

    static IBreakpointRegistry CreateBreakpointRegistry() => new BreakpointRegistry();

    static IWorkflowDebugger CreateWorkflowDebugger(IBreakpointRegistry breakpoints, ILoggerFactory loggerFactory, IDefault<IDebugFrontend> defaultFrontend) =>
        new WorkflowDebugger(breakpoints, loggerFactory, defaultFrontend);
}
