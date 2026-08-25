using Cyborg.Core.Modules.Runtime.Environments;
using Microsoft.Extensions.Logging;

namespace Cyborg.Core.Modules.Runtime;

public sealed class RootModuleRuntime(GlobalRuntimeEnvironment defaultEnvironment, ILoggerFactory loggerFactory, IServiceProvider? serviceProvider = null)
    : ModuleRuntimeBase(defaultEnvironment.SyntaxFactory, loggerFactory, serviceProvider)
{
    private readonly Dictionary<string, IRuntimeEnvironment> _environments = [];

    public override IRuntimeEnvironment GlobalEnvironment { get; } = defaultEnvironment;

    public override IRuntimeEnvironment ParentEnvironment => GlobalEnvironment;

    public override IRuntimeEnvironment Environment => GlobalEnvironment;

    protected override IModuleRuntime? Parent => null;

    public override bool TryGetEnvironment(string name, [NotNullWhen(true)] out IRuntimeEnvironment? environment)
    {
        if (Environment.Name.Equals(name, StringComparison.Ordinal))
        {
            environment = Environment;
            return true;
        }
        return _environments.TryGetValue(name, out environment);
    }

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

    protected override Task<IModuleExecutionResult> ExecuteWorkerAsync(IModuleWorker module, EnvironmentScope scope, string? name, CancellationToken cancellationToken)
    {
        IRuntimeEnvironment environment = CreateScopedEnvironment(parent: this, scope, name);
        return ExecuteWorkerAsync(module, environment, cancellationToken);
    }

    protected override Task<IModuleExecutionResult> ExecuteWorkerAsync(IModuleWorker module, IRuntimeEnvironment environment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(module);
        return ExecuteModuleAsync(root: this, module, environment, cancellationToken);
    }
}
