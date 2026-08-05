namespace Cyborg.Core.Aot.Modules.Validation.Attributes;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = true, Inherited = true)]
internal sealed class MaxLengthAttribute(int max) : PropertyValidationAttribute
{
    public int Max { get; } = max;
}
