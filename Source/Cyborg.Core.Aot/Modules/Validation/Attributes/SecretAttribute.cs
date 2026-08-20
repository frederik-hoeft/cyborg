namespace Cyborg.Core.Aot.Modules.Validation.Attributes;

/// <summary>
/// Injects and asserts the <c>cyborg.secret.v1</c> tag on a <c>TaggedString</c> property.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
internal sealed class SecretAttribute : Attribute;
