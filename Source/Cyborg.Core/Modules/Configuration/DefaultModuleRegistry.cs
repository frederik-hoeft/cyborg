using Cyborg.Core.Modules.Configuration.Model;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Modules.Configuration;

public sealed class DefaultModuleRegistry : IModuleRegistry
{
    private readonly Dictionary<string, ModuleContext> _modules = [];

    public bool TryAddModule(string name, ModuleContext module) => _modules.TryAdd(name, module);

    public bool TryGetModule(string name, [NotNullWhen(true)] out ModuleContext? module) => _modules.TryGetValue(name, out module);

    public bool TryRemoveModule(string name) => _modules.Remove(name);
}
