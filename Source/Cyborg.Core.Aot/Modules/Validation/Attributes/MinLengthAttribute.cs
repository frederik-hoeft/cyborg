namespace Cyborg.Core.Aot.Modules.Validation.Attributes;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = true, Inherited = true)]
internal sealed class MinLengthAttribute(int min) : PropertyValidationAttribute
{
    public int Min { get; } = min;
}
