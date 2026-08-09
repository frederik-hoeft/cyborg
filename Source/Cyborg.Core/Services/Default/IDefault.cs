namespace Cyborg.Core.Services.Default;

public interface IDefault<TService> where TService : class, IKeyedService
{
    string ConfigurationKey { get; }

    TService? GetDefault();

    TService GetRequiredDefault();
}
