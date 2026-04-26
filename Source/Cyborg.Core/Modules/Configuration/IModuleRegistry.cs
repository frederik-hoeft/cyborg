using Cyborg.Core.Modules.Configuration.Model;

namespace Cyborg.Core.Modules.Configuration;

public interface IModuleRegistry
{
    bool TryAddModule(string name, ModuleContext module);

    bool TryRemoveModule(string name);

    bool TryGetModule(string name, [NotNullWhen(true)] out ModuleContext? module);
}
