using Cyborg.Core.Aot.Contracts;
using Cyborg.Core.Modules.Descriptors;
using Cyborg.Core.Modules.Runtime;
using System.Text.Json.Serialization;

namespace Cyborg.Core.Modules;

[GeneratorContractRegistration<ModuleLoaderFactoryGeneratorContract>(ModuleLoaderFactoryGeneratorContract.IModuleWorker)]
public interface IModuleWorker
{
    string ModuleId { get; }

    [JsonIgnore]
    IModule Module { get; }

    internal Task<IModuleDescriptor> PrepareAsync(IModuleRuntime runtime, CancellationToken cancellationToken);

    internal Task<IModuleExecutionResult> ExecuteAsync(IModuleRuntime runtime, CancellationToken cancellationToken);
}
