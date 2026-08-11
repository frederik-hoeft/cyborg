namespace Cyborg.Core.Services.Default;

public sealed record ServiceSelectionKey<TService>(string Key, string? DefaultService = null) : IServiceSelectionKey<TService> where TService : class, IKeyedService;
