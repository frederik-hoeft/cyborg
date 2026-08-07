namespace Cyborg.Core.Services.Default;

public interface IServiceSelectionKey<TService> where TService : class, IKeyedService
{
    string Key { get; }

    string? DefaultService { get; }
}
