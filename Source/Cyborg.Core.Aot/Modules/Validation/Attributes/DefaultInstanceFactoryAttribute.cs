namespace Cyborg.Core.Aot.Modules.Validation.Attributes;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
internal sealed class DefaultInstanceFactoryAttribute(string factoryMethod) : Attribute
{
    public string FactoryMethod { get; } = factoryMethod;
}
