using Cyborg.Core.Modules.Runtime.Environments;

namespace Cyborg.Core.Modules.Runtime;

public sealed class RootModuleRuntime(GlobalRuntimeEnvironment defaultEnvironment) : ModuleRuntimeBase(defaultEnvironment.SyntaxFactory)
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

    public override Task<IModuleExecutionResult> ExecuteAsync(IModuleWorker module, EnvironmentScope scope = EnvironmentScope.Global, string? name = null, CancellationToken cancellationToken = default)
    {
        IRuntimeEnvironment environment = CreateScopedEnvironment(parent: this, scope, name);
        return ExecuteAsync(module, environment, cancellationToken);
    }

    public async override Task<IModuleExecutionResult> ExecuteAsync(IModuleWorker module, IRuntimeEnvironment environment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(module);
        IRuntimeEnvironment boundEnvironment = environment.Bind(module);
        IModuleRuntime runtime = new ScopedRuntime(root: this, parent: this, boundEnvironment, SyntaxFactory);
        IModuleExecutionResult result = await module.ExecuteAsync(runtime, cancellationToken);
        return result;
    }
}