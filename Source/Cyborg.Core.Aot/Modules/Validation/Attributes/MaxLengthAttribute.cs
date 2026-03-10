namespace Cyborg.Core.Aot.Modules.Validation.Attributes;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
internal sealed class MaxLengthAttribute(int max) : Attribute
{
    public int Max { get; } = max;
}
