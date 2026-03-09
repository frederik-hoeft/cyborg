namespace Cyborg.Core.Aot.Modules.Validation.Attributes;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class RangeAttribute<T> : Attribute
{
    public T? Min { get; set; }
    public T? Max { get; set; }
}