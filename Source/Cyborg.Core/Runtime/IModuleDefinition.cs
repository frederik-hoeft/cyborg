namespace Cyborg.Core.Runtime;

public interface IModuleDefinition : IModule
{
    static abstract string ModuleId { get; }
}
