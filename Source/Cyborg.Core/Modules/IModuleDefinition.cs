namespace Cyborg.Core.Modules;

public interface IModuleDefinition : IModule
{
    static abstract string ModuleId { get; }
}
