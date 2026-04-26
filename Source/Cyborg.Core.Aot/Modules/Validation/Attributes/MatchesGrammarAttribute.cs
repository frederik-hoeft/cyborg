namespace Cyborg.Core.Aot.Modules.Validation.Attributes;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
internal sealed class MatchesGrammarAttribute(string parserMemberName) : Attribute
{
    public string ParserMemberName { get; } = parserMemberName;
}
