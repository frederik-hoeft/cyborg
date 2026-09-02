using Cyborg.Core.Configuration.Serialization.Dynamics;
using Cyborg.Core.Runtime.Hooks;
using Cyborg.Core.Runtime.Services.Debugging.Breakpoints;
using Cyborg.Core.Runtime.Services.Debugging.Configuration;
using Cyborg.Core.Runtime.Services.Transactions;
using Cyborg.Core.Services.Default;
using Jab;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cyborg.Core.Runtime.Services.Debugging;

[ServiceProviderModule]
[Singleton<IDynamicValueProvider>(Factory = nameof(CreateDebugOptionsProvider))]
[Singleton<IBreakpointRegistry>(Factory = nameof(CreateBreakpointRegistry))]
[Singleton<IDebugSessionState>(Factory = nameof(CreateDebugSessionState))]
[Singleton<TransactionalServiceParticipant>(Factory = nameof(CreateDebugBranchControlParticipant))]
[Scoped<IDebugBranchControl>(Factory = nameof(CreateDebugBranchControl))]
[Singleton<IWorkflowDebugger>(Factory = nameof(CreateWorkflowDebugger))]
[Singleton<IDebugExecutionTopology>(Factory = nameof(CreateExecutionTopology))]
[Singleton<IServiceSelectionKey<IDebugFrontend>>(Instance = nameof(DebugFrontendSelectionKey))]
[Singleton<IDefault<IDebugFrontend>, Default<IDebugFrontend>>]
[Singleton<IModulePreExecutionHook>(Factory = nameof(CreateDebuggingHook))]
[Singleton<IModuleExecutionLifecycleHook>(Factory = nameof(CreateExecutionTopologyHook))]
public interface IDebugServices
{
    static ServiceSelectionKey<IDebugFrontend> DebugFrontendSelectionKey => new("cyborg.core.debug.frontend", DebugOptions.Default.Frontend);

    static IDynamicValueProvider CreateDebugOptionsProvider() => new DebugOptionsProvider();

    static IBreakpointRegistry CreateBreakpointRegistry() => new BreakpointRegistry();

    static IDebugSessionState CreateDebugSessionState() => new DebugSessionState();

    static TransactionalServiceParticipant CreateDebugBranchControlParticipant(IDebugSessionState sessionState)
    {
        ArgumentNullException.ThrowIfNull(sessionState);
        return new DebugBranchControlParticipant(sessionState);
    }

    static IDebugBranchControl CreateDebugBranchControl(ITransactionalServiceContext context, IDebugSessionState sessionState)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(sessionState);
        return new DebugBranchControl(context, sessionState);
    }

    static IWorkflowDebugger CreateWorkflowDebugger(
        IBreakpointRegistry breakpoints,
        ILoggerFactory loggerFactory,
        IDefault<IDebugFrontend> defaultFrontend,
        IDebugExecutionTopology topology,
        IDebugSessionState sessionState)
    {
        ArgumentNullException.ThrowIfNull(breakpoints);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(defaultFrontend);
        ArgumentNullException.ThrowIfNull(topology);
        ArgumentNullException.ThrowIfNull(sessionState);
        return new WorkflowDebugger(breakpoints, loggerFactory, defaultFrontend, topology, sessionState);
    }

    static IDebugExecutionTopology CreateExecutionTopology() => new DebugExecutionTopology();

    static IModuleExecutionLifecycleHook CreateExecutionTopologyHook(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        return (IDebugExecutionTopologyController)serviceProvider.GetRequiredService<IDebugExecutionTopology>();
    }

    static IModulePreExecutionHook CreateDebuggingHook(
        IServiceProvider serviceProvider,
        IWorkflowDebugger? debugger = null)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        IDebugExecutionTopologyController topology =
            (IDebugExecutionTopologyController)serviceProvider.GetRequiredService<IDebugExecutionTopology>();
        return new DebuggingHook(serviceProvider, topology, debugger);
    }
}
