namespace Cyborg.Core.Services;

public interface INamedServiceProvider
{
    T? GetService<T>(string name) where T : class;
}