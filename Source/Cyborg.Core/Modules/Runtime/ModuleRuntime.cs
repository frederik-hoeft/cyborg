using Cyborg.Core.Modules.Runtime.Environments;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Modules.Runtime;

public sealed class ModuleRuntime(GlobalRuntimeEnvironment defaultEnvironment) : ModuleRuntimeBase
{
    private readonly Dictionary<string, IRuntimeEnvironment> _environments = new()
    {
        { defaultEnvironment.Name, defaultEnvironment }
    };

    public override IRuntimeEnvironment GlobalEnvironment { get; } = defaultEnvironment;

    public override IRuntimeEnvironment ParentEnvironment => GlobalEnvironment;

    public override IRuntimeEnvironment Environment => GlobalEnvironment;

    public override bool TryGetEnvironment(string name, [NotNullWhen(true)] out IRuntimeEnvironment? environment) =>
        _environments.TryGetValue(name, out environment);

    public override bool TryAddEnvironment(IRuntimeEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        if (environment.IsTransient || _environments.ContainsKey(environment.Name))
        {
            return false;
        }
        _environments.Add(environment.Name, environment);
        return true;
    }

    public override bool TryRemoveEnvironment(IRuntimeEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        return _environments.Remove(environment.Name);
    }

    public override Task<bool> ExecuteAsync(IModuleWorker module, EnvironmentScope scope = EnvironmentScope.Global, string? name = null, CancellationToken cancellationToken = default)
    {
        IRuntimeEnvironment environment = CreateScopedEnvironment(parent: this, scope, name);
        return ExecuteAsync(module, environment, cancellationToken);
    }

    public override Task<bool> ExecuteAsync(IModuleWorker module, IRuntimeEnvironment environment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(module);
        IModuleRuntime runtime = new ScopedRuntime(root: this, parent: this, environment: environment);
        return module.ExecuteAsync(runtime, cancellationToken);
    }
}