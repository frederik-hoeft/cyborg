namespace Cyborg.Core.Aot.Modules.Validation.Attributes;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
internal sealed class RangeAttribute<T> : Attribute
{
    public T? Min { get; set; }
    public T? Max { get; set; }
}