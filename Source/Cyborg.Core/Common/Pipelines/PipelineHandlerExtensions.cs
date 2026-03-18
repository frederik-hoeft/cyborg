using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Common.Pipelines;

[SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "False positive for C# 14 extension syntax.")]
public static class PipelineHandlerExtensions
{
    extension<T>(IEnumerable<T> self) where T : class, IPipelineHandler
    {
        public ImmutableArray<T> CreatePipeline() => [.. self.InPipelineOrder()];

        public IEnumerable<T> InPipelineOrder() => self.OrderBy(handler => handler.Priority);
    }
}