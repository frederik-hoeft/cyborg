namespace Cyborg.Core.Aot.Json.Configuration;

[AttributeUsage(AttributeTargets.Class)]
public sealed class JsonTypeInfoBindingsGeneratorAttribute : Attribute
{
    public BindingsGenerationMode GenerationMode { get; set; } = BindingsGenerationMode.Safe;
}
