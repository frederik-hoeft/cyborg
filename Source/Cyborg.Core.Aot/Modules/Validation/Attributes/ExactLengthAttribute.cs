namespace Cyborg.Core.Aot.Modules.Validation.Attributes;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
internal sealed class ExactLengthAttribute(int length) : Attribute
{
    public int Length { get; } = length;
}
