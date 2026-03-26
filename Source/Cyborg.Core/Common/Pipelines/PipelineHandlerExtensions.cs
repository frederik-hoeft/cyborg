using System.Collections.Immutable;

namespace Cyborg.Core.Common.Pipelines;

[SuppressMessage("Design", CA1034, Justification = CA1034_JUSTIFY_EXTENSION_SYNTAX_CSHARP_14)]
public static class PipelineHandlerExtensions
{
    extension<T>(IEnumerable<T> self) where T : class, IPipelineHandler
    {
        public ImmutableArray<T> CreatePipeline() => [.. self.InPipelineOrder()];

        public IEnumerable<T> InPipelineOrder() => self.OrderBy(handler => handler.Priority);
    }
}