using Cyborg.Core.Modules.Runtime.Environements;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Modules.Runtime;

public interface IModuleRuntime
{
    IRuntimeEnvironment GlobalEnvironment { get; }

    IRuntimeEnvironment Environment { get; }

    bool TryGetEnvironment(string name, [NotNullWhen(true)] out IRuntimeEnvironment? environment);

    bool TryAddEnvironment(IRuntimeEnvironment environment);

    bool TryRemoveEnvironment(IRuntimeEnvironment environment);

    Task<bool> ExecuteAsync(IModuleWorker module, EnvironmentScope scope = EnvironmentScope.Global, string? name = null, CancellationToken cancellationToken = default);

    Task<bool> ExecuteAsync(IModuleWorker module, IRuntimeEnvironment environment, CancellationToken cancellationToken = default);
}