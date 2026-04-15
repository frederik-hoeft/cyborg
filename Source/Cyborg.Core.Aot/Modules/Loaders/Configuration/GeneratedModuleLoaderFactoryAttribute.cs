namespace Cyborg.Core.Aot.Modules.Loaders.Configuration;

[AttributeUsage(AttributeTargets.Class)]
internal sealed class GeneratedModuleLoaderFactoryAttribute : Attribute
{
    public string? Name { get; set; }
}