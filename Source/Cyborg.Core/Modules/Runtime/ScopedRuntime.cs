using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Modules.Runtime.Environments.Syntax;
using Microsoft.Extensions.Logging;

namespace Cyborg.Core.Modules.Runtime;

internal sealed class ScopedRuntime(IModuleRuntime root, IModuleRuntime parent, IRuntimeEnvironment environment, VariableSyntaxBuilder syntaxFactory, ILoggerFactory loggerFactory) : ModuleRuntimeBase(syntaxFactory, loggerFactory)
{
    public override IRuntimeEnvironment GlobalEnvironment => root.GlobalEnvironment;

    public override IRuntimeEnvironment ParentEnvironment => parent.Environment;

    public override IRuntimeEnvironment Environment => environment;

    [NotNull]
    protected override IModuleRuntime? Parent => parent;

    public override Task<IModuleExecutionResult> ExecuteAsync(IModuleWorker module, EnvironmentScope scope = EnvironmentScope.Global, string? name = null, CancellationToken cancellationToken = default)
    {
        IRuntimeEnvironment scopedEnvironment = CreateScopedEnvironment(parent: this, scope, name);
        return ExecuteAsync(module, scopedEnvironment, cancellationToken);
    }

    public async override Task<IModuleExecutionResult> ExecuteAsync(IModuleWorker module, IRuntimeEnvironment moduleEnvironment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(module);
        IRuntimeEnvironment boundEnvironment = moduleEnvironment.Bind(module);
        IModuleRuntime runtime = new ScopedRuntime(root, parent: this, boundEnvironment, SyntaxFactory, LoggerFactory);
        Logger.LogModuleDispatched(module.ModuleId, boundEnvironment.Name);
        IModuleExecutionResult result = await module.ExecuteAsync(runtime, cancellationToken);
        if (result.Status is ModuleExitStatus.Failed or ModuleExitStatus.Canceled)
        {
            Logger.LogModuleExecutionFailed(module.ModuleId, result.Status.ToString(), boundEnvironment.Name);
        }
        else
        {
            Logger.LogModuleCompleted(module.ModuleId, result.Status.ToString(), boundEnvironment.Name);
        }
        return result;
    }

    public override bool TryAddEnvironment(IRuntimeEnvironment runtimeEnvironment) => root.TryAddEnvironment(runtimeEnvironment);

    public override bool TryGetEnvironment(string name, [NotNullWhen(true)] out IRuntimeEnvironment? runtimeEnvironment)
    {
        if (Environment.Name.Equals(name, StringComparison.Ordinal))
        {
            runtimeEnvironment = Environment;
            return true;
        }
        return parent.TryGetEnvironment(name, out runtimeEnvironment);
    }

    public override bool TryRemoveEnvironment(IRuntimeEnvironment runtimeEnvironment) => root.TryRemoveEnvironment(runtimeEnvironment);
}