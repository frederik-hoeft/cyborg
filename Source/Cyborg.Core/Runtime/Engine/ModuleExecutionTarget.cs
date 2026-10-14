using Cyborg.Core.Runtime.Configuration;
using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Model;

namespace Cyborg.Core.Runtime.Engine;

/// <summary>
/// Identifies what an invocation will execute, without delegating dispatch back to the runtime facade.
/// All target forms share the same invocation lifetime but differ in their preparation and dispatch steps.
/// </summary>
internal sealed class ModuleExecutionTarget
{
    private ModuleExecutionTarget(ModuleExecutionKind kind, string moduleId, IModule module, IRuntimeEnvironment environment, CancellationToken cancellationToken)
    {
        Kind = kind;
        ModuleId = moduleId;
        Module = module;
        Environment = environment;
        CancellationToken = cancellationToken;
    }

    public ModuleExecutionKind Kind { get; }

    public string ModuleId { get; }

    public IModule Module { get; }

    public IRuntimeEnvironment Environment { get; }

    public CancellationToken CancellationToken { get; }

    public ModuleContext? Context { get; private init; }

    public ModuleConfigurationLoadResult? Configuration { get; private init; }

    public ModuleReference? Reference { get; private init; }

    public IModuleWorker? Worker { get; private init; }

    public static ModuleExecutionTarget ForContext(ModuleContext context, IRuntimeEnvironment environment, CancellationToken cancellationToken) =>
        new(ModuleExecutionKind.Context, context.Module.ModuleId, context.Module.Definition, environment, cancellationToken) { Context = context };

    public static ModuleExecutionTarget ForConfiguration(ModuleConfigurationLoadResult configuration, IRuntimeEnvironment environment, CancellationToken cancellationToken) =>
        new(ModuleExecutionKind.Configuration, configuration.ModuleContext.Module.ModuleId, configuration.ModuleContext.Module.Definition, environment, cancellationToken)
        { Configuration = configuration };

    public static ModuleExecutionTarget ForRootModule(ModuleConfigurationLoadResult configuration, IRuntimeEnvironment environment, CancellationToken cancellationToken) =>
        new(ModuleExecutionKind.RootModule, configuration.ModuleContext.Module.ModuleId, configuration.ModuleContext.Module.Definition, environment, cancellationToken)
        { Configuration = configuration };

    public static ModuleExecutionTarget ForReference(ModuleReference reference, IRuntimeEnvironment environment, CancellationToken cancellationToken) =>
        new(ModuleExecutionKind.Reference, reference.ModuleId, reference.Definition, environment, cancellationToken) { Reference = reference };

    public static ModuleExecutionTarget ForWorker(IModuleWorker worker, IRuntimeEnvironment environment, CancellationToken cancellationToken) =>
        new(ModuleExecutionKind.ActivatedWorker, worker.ModuleId, worker.Module, environment, cancellationToken) { Worker = worker };
}

internal enum ModuleExecutionKind
{
    Context,
    Configuration,
    RootModule,
    Reference,
    ActivatedWorker,
}
