namespace Cyborg.Core.Services.Pipelines;

public interface IServicePipeline<out TService> : IEnumerable<TService> where TService : class, IPipelineHandler;
