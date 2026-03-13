using Cyborg.Core.Modules.Runtime.Environments;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Cyborg.Core.Modules.Runtime;

internal sealed class ScopedRuntime(IModuleRuntime root, IModuleRuntime parent, IRuntimeEnvironment environment, JsonNamingPolicy namingPolicy) : ModuleRuntimeBase(namingPolicy)
{
    public override IRuntimeEnvironment GlobalEnvironment => root.GlobalEnvironment;

    public override IRuntimeEnvironment ParentEnvironment => parent.Environment;

    public override IRuntimeEnvironment Environment => environment;

    public override Task<IModuleExecutionResult> ExecuteAsync(IModuleWorker module, EnvironmentScope scope = EnvironmentScope.Global, string? name = null, CancellationToken cancellationToken = default)
    {
        IRuntimeEnvironment environment = CreateScopedEnvironment(parent: this, scope, name);
        return ExecuteAsync(module, environment, cancellationToken);
    }

    public async override Task<IModuleExecutionResult> ExecuteAsync(IModuleWorker module, IRuntimeEnvironment environment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(module);
        using SelfReferenceScope _ = environment.EnterSelfReferenceScope(module);
        IModuleRuntime runtime = new ScopedRuntime(root, parent: this, environment, NamingPolicy);
        IModuleExecutionResult result = await module.ExecuteAsync(runtime, cancellationToken);
        return result;
    }

    public override bool TryAddEnvironment(IRuntimeEnvironment environment) => root.TryAddEnvironment(environment);

    public override bool TryGetEnvironment(string name, [NotNullWhen(true)] out IRuntimeEnvironment? environment) => root.TryGetEnvironment(name, out environment);

    public override bool TryRemoveEnvironment(IRuntimeEnvironment environment) => root.TryRemoveEnvironment(environment);
}