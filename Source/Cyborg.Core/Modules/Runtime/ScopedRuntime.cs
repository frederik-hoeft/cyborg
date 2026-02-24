using Cyborg.Core.Modules.Runtime.Environements;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Modules.Runtime;

internal sealed class ScopedRuntime(IModuleRuntime root, IModuleRuntime parent, IRuntimeEnvironment environment) : IModuleRuntime
{
    public IRuntimeEnvironment GlobalEnvironment => root.GlobalEnvironment;

    public IRuntimeEnvironment Environment => environment;

    public Task<bool> ExecuteAsync(IModuleWorker module, EnvironmentScope scope = EnvironmentScope.Global, string? name = null, CancellationToken cancellationToken = default)
    {
        IRuntimeEnvironment environment = ModuleRuntime.CreateScopedEnvironment(parent: this, scope, name);
        return ExecuteAsync(module, environment, cancellationToken);
    }

    public Task<bool> ExecuteAsync(IModuleWorker module, IRuntimeEnvironment environment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(module);
        IModuleRuntime runtime = new ScopedRuntime(root, parent: this, environment);
        return module.ExecuteAsync(runtime, cancellationToken);
    }

    public bool TryAddEnvironment(IRuntimeEnvironment environment) => root.TryAddEnvironment(environment);

    public bool TryGetEnvironment(string name, [NotNullWhen(true)] out IRuntimeEnvironment? environment) => root.TryGetEnvironment(name, out environment);

    public bool TryRemoveEnvironment(IRuntimeEnvironment environment) => root.TryRemoveEnvironment(environment);
}