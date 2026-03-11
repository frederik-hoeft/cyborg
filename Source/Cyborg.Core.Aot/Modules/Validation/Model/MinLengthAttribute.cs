namespace Cyborg.Core.Aot.Modules.Validation.Model;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
internal sealed class MinLengthAttribute(int min) : Attribute
{
    public int Min { get; } = min;
}
