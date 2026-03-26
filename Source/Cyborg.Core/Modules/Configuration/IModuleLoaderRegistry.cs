namespace Cyborg.Core.Modules.Configuration;

public interface IModuleLoaderRegistry
{
    bool TryGetModuleLoader(string name, [NotNullWhen(true)] out IModuleLoader? moduleLoader);
}
