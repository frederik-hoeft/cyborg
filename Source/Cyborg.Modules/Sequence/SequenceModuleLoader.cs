using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Modules.Configuration;

namespace Cyborg.Modules.Sequence;

[GeneratedModuleLoaderFactory]
public sealed partial class SequenceModuleLoader(IServiceProvider serviceProvider) : ModuleLoader<SequenceModuleWorker, SequenceModule>(serviceProvider);
