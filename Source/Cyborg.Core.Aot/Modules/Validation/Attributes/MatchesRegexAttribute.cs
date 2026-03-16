namespace Cyborg.Core.Aot.Modules.Validation.Attributes;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
internal sealed class MatchesRegexAttribute(string regexMemberName) : Attribute
{
    public string RegexMemberName { get; } = regexMemberName;
}