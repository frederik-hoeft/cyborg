namespace Cyborg.Core.Services.Default;

public interface IDefault<TService> where TService : class, IKeyedService
{
    TService? GetDefault();

    TService GetRequiredDefault();
}
