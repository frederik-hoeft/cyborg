namespace Cyborg.Core.Aot.Modules.Validation.Attributes;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = true, Inherited = true)]
internal sealed class LengthAttribute(int min, int max) : PropertyValidationAttribute
{
    public int Min { get; } = min;

    public int Max { get; } = max;
}
