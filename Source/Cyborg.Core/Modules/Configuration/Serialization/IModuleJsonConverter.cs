namespace Cyborg.Core.Modules.Configuration.Serialization;

public interface IModuleJsonConverter
{
    internal protected IModuleLoaderContext Context { get; internal set; }
}
