namespace Cyborg.Core.Aot.Modules.Validation.Attributes;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
internal sealed class LengthAttribute(int min, int max) : Attribute
{
    public int Min { get; } = min;

    public int Max { get; } = max;
}