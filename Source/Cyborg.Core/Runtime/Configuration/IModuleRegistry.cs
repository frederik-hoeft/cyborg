using Cyborg.Core.Runtime.Model;

namespace Cyborg.Core.Runtime.Configuration;

public interface IModuleRegistry
{
    bool TryAddModule(string name, ModuleContext module);

    bool TryRemoveModule(string name);

    bool TryGetModule(string name, [NotNullWhen(true)] out ModuleContext? module);
}
