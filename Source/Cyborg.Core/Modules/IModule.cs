namespace Cyborg.Core.Modules;

public interface IModule
{
    string? Name { get; }

    string? Group { get; }

    static abstract string ModuleId { get; }
}
