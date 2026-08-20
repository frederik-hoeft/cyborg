namespace Cyborg.Core.Aot.Modules.Validation.Attributes;

/// <summary>
/// Marks a string property as intentionally untagged. Suppresses the diagnostic that suggests
/// migrating the property to <c>TaggedString</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
internal sealed class UntaggedAttribute : Attribute;
