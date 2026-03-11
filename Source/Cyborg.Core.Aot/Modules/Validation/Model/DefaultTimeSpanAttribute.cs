namespace Cyborg.Core.Aot.Modules.Validation.Model;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
internal sealed class DefaultTimeSpanAttribute(string timeSpan) : Attribute
{
    public string TimeSpan { get; } = timeSpan;
}