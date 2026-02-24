using Cyborg.Core.Modules.Runtime.Environements;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Modules.Runtime;

public sealed class ModuleRuntime(GlobalRuntimeEnvironment defaultEnvironment) : IModuleRuntime
{
    private readonly Dictionary<string, IRuntimeEnvironment> _environments = new()
    {
        { defaultEnvironment.Name, defaultEnvironment }
    };

    public IRuntimeEnvironment GlobalEnvironment { get; } = defaultEnvironment;

    public IRuntimeEnvironment Environment => GlobalEnvironment;

    public bool TryGetEnvironment(string name, [NotNullWhen(true)] out IRuntimeEnvironment? environment) =>
        _environments.TryGetValue(name, out environment);

    public bool TryAddEnvironment(IRuntimeEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        if (environment.IsTransient || _environments.ContainsKey(environment.Name))
        {
            return false;
        }
        _environments.Add(environment.Name, environment);
        return true;
    }

    public bool TryRemoveEnvironment(IRuntimeEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        return _environments.Remove(environment.Name);
    }

    public Task<bool> ExecuteAsync(IModuleWorker module, EnvironmentScope scope = EnvironmentScope.Global, string? name = null, CancellationToken cancellationToken = default)
    {
        IRuntimeEnvironment environment = CreateScopedEnvironment(parent: this, scope, name);
        return ExecuteAsync(module, environment, cancellationToken);
    }

    public Task<bool> ExecuteAsync(IModuleWorker module, IRuntimeEnvironment environment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(module);
        IModuleRuntime runtime = new ScopedRuntime(root: this, parent: this, environment: environment);
        return module.ExecuteAsync(runtime, cancellationToken);
    }

    internal static IRuntimeEnvironment CreateScopedEnvironment(IModuleRuntime parent, EnvironmentScope scope, string? name)
    {
        bool transient = false;
        if (string.IsNullOrEmpty(name))
        {
            transient = true;
            name = Guid.CreateVersion7().ToString();
        }
        IRuntimeEnvironment environment = scope switch
        {
            EnvironmentScope.Isolated => new RuntimeEnvironment(name, transient),
            EnvironmentScope.Global => parent.GlobalEnvironment,
            EnvironmentScope.InheritParent => new InheritedRuntimeEnvironment(name, parent.Environment, transient),
            EnvironmentScope.InheritGlobal => new InheritedRuntimeEnvironment(name, parent.GlobalEnvironment, transient),
            EnvironmentScope.Parent => parent.Environment,
            EnvironmentScope.Reference => throw new ArgumentException("Attempting to create an environment by reference without providing an environment reference.", nameof(scope)),
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Invalid environment scope.")
        };
        parent.TryAddEnvironment(environment);
        return environment;
    }
}