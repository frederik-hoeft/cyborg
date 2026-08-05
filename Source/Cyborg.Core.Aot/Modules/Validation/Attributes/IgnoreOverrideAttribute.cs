namespace Cyborg.Core.Aot.Modules.Validation.Attributes;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
internal sealed class IgnoreOverrideAttribute(bool recurse = false) : Attribute
{
    public bool Recurse { get; } = recurse;
}
