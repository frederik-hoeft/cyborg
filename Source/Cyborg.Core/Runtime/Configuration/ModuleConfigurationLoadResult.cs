using Cyborg.Core.Runtime.Model;

namespace Cyborg.Core.Runtime.Configuration;

public sealed class ModuleConfigurationLoadResult
{
    public ModuleContext ModuleContext { get; }

    internal ModuleRegistrySeed RegistrySeed { get; }

    public ModuleConfigurationLoadResult(ModuleContext moduleContext) : this(moduleContext, ModuleRegistrySeed.Empty)
    {
    }

    internal ModuleConfigurationLoadResult(ModuleContext moduleContext, ModuleRegistrySeed registrySeed)
    {
        ArgumentNullException.ThrowIfNull(moduleContext);
        ArgumentNullException.ThrowIfNull(registrySeed);
        ModuleContext = moduleContext;
        RegistrySeed = registrySeed;
    }
}
