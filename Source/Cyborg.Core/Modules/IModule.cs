namespace Cyborg.Core.Modules;

public interface IModule
{
    string? Name { get; }

    static abstract string ModuleId { get; }
}