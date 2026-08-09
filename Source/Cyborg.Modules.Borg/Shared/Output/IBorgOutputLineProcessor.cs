using Cyborg.Core.Services.Pipelines;
using Cyborg.Modules.Borg.Shared.Json.Logging;

namespace Cyborg.Modules.Borg.Shared.Output;

public interface IBorgOutputLineProcessor : IPipelineHandler
{
    ValueTask<bool> TryProcessLineAsync(BorgJsonLine line, CancellationToken cancellationToken);
}
