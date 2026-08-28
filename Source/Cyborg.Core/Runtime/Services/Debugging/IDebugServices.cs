using Cyborg.Core.Configuration.Serialization.Dynamics;
using Cyborg.Core.Runtime.Hooks;
using Cyborg.Core.Runtime.Services.Debugging.Breakpoints;
using Cyborg.Core.Runtime.Services.Debugging.Configuration;
using Cyborg.Core.Services.Default;
using Jab;
using Microsoft.Extensions.Logging;

namespace Cyborg.Core.Runtime.Services.Debugging;

[ServiceProviderModule]
[Singleton<IDynamicValueProvider>(Factory = nameof(CreateDebugOptionsProvider))]
[Singleton<IBreakpointRegistry>(Factory = nameof(CreateBreakpointRegistry))]
[Singleton<IWorkflowDebugger>(Factory = nameof(CreateWorkflowDebugger))]
[Singleton<IServiceSelectionKey<IDebugFrontend>>(Instance = nameof(DebugFrontendSelectionKey))]
[Singleton<IDefault<IDebugFrontend>, Default<IDebugFrontend>>]
[Singleton<IModulePreExecutionHook>(Factory = nameof(CreateDebuggingHook))]
public interface IDebugServices
{
    static ServiceSelectionKey<IDebugFrontend> DebugFrontendSelectionKey => new("cyborg.core.debug.frontend", DebugOptions.Default.Frontend);

    static IDynamicValueProvider CreateDebugOptionsProvider() => new DebugOptionsProvider();

    static IBreakpointRegistry CreateBreakpointRegistry() => new BreakpointRegistry();

    static IWorkflowDebugger CreateWorkflowDebugger(IBreakpointRegistry breakpoints, ILoggerFactory loggerFactory, IDefault<IDebugFrontend> defaultFrontend)
    {
        ArgumentNullException.ThrowIfNull(breakpoints);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(defaultFrontend);
        return new WorkflowDebugger(breakpoints, loggerFactory, defaultFrontend);
    }

    static IModulePreExecutionHook CreateDebuggingHook(IServiceProvider serviceProvider, IWorkflowDebugger? debugger = null)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        return new DebuggingHook(serviceProvider, debugger);
    }
}
