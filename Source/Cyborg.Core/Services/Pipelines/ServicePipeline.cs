using System.Collections;

namespace Cyborg.Core.Services.Pipelines;

public sealed class ServicePipeline<TService>(IEnumerable<TService> services) : IServicePipeline<TService> where TService : class, IPipelineHandler
{
    private readonly IReadOnlyCollection<TService> _servicePipeline = [.. services.InPipelineOrder()];

    public IEnumerator<TService> GetEnumerator() => _servicePipeline.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
