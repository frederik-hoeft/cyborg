namespace Cyborg.Core.Aot.Modules.Validation.Attributes;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = true, Inherited = true)]
internal sealed class ExactLengthAttribute(int length) : PropertyValidationAttribute
{
    public int Length { get; } = length;
}
