using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Modules.Runtime.Environments.Syntax;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Modules.Runtime;

internal sealed class ScopedRuntime(IModuleRuntime root, IModuleRuntime parent, IRuntimeEnvironment environment, VariableSyntaxBuilder syntaxFactory) : ModuleRuntimeBase(syntaxFactory)
{
    public override IRuntimeEnvironment GlobalEnvironment => root.GlobalEnvironment;

    public override IRuntimeEnvironment ParentEnvironment => parent.Environment;

    public override IRuntimeEnvironment Environment => environment;

    [NotNull]
    protected override IModuleRuntime? Parent => parent;

    public override Task<IModuleExecutionResult> ExecuteAsync(IModuleWorker module, EnvironmentScope scope = EnvironmentScope.Global, string? name = null, CancellationToken cancellationToken = default)
    {
        IRuntimeEnvironment environment = CreateScopedEnvironment(parent: this, scope, name);
        return ExecuteAsync(module, environment, cancellationToken);
    }

    public async override Task<IModuleExecutionResult> ExecuteAsync(IModuleWorker module, IRuntimeEnvironment environment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(module);
        IRuntimeEnvironment boundEnvironment = environment.Bind(module);
        IModuleRuntime runtime = new ScopedRuntime(root, parent: this, boundEnvironment, SyntaxFactory);
        IModuleExecutionResult result = await module.ExecuteAsync(runtime, cancellationToken);
        return result;
    }

    public override bool TryAddEnvironment(IRuntimeEnvironment environment) => root.TryAddEnvironment(environment);

    public override bool TryGetEnvironment(string name, [NotNullWhen(true)] out IRuntimeEnvironment? environment) => root.TryGetEnvironment(name, out environment);

    public override bool TryRemoveEnvironment(IRuntimeEnvironment environment) => root.TryRemoveEnvironment(environment);
}