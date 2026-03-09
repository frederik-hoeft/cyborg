namespace Cyborg.Core.Aot.Modules.Validation.Attributes;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class DefaultValueAttribute<T>(T value, params T[] whenPresent) : Attribute
{
    public T Value { get; } = value;

    public T[] WhenPresent { get; } = whenPresent;
}