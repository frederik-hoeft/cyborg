namespace Cyborg.Core.Runtime.Configuration;

public interface IModuleLoaderRegistry
{
    bool TryGetModuleLoader(string name, [NotNullWhen(true)] out IModuleLoader? moduleLoader);
}
