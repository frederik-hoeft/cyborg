using Cyborg.Core.Modules.Runtime;

namespace Cyborg.Core.Modules;

public interface IModuleWorker
{
    string ModuleId { get; }

    internal Task<bool> ExecuteAsync(IModuleRuntime runtime, CancellationToken cancellationToken);
}
