namespace Cyborg.Core.Aot.Modules.Validation.Attributes;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class DefaultTimeSpanAttribute(string timeSpan) : Attribute
{
    public string TimeSpan { get; } = timeSpan;
}