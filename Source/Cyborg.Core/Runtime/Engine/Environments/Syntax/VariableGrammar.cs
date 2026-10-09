namespace Cyborg.Core.Runtime.Engine.Environments.Syntax;

/// <summary>
/// Defines the grammar for variable identifiers, namespaces, and value expressions used in the environment.
/// </summary>
internal static class VariableGrammar
{
    [StringSyntax(StringSyntaxAttribute.Regex)]
    private const string DELIMITER_CHARS = @"[\.]";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    private const string IDENTIFIER_PREFIX = @"[A-Za-z_\-]";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    private const string IDENTIFIER_CHARS = @"[A-Za-z_0-9\-]";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    private const string IDENTIFIER = $@"{IDENTIFIER_PREFIX}{IDENTIFIER_CHARS}*(?:{DELIMITER_CHARS}{IDENTIFIER_CHARS}+)*";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string IDENTIFIER_PATTERN = $@"\A{IDENTIFIER}\z";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    // currently the same as IDENTIFIER_PATTERN, but may diverge in the future
    public const string NAMESPACE_PATTERN = $@"\A{IDENTIFIER}\z";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    // @@ late self, @ self, @identifier entry-point reference, or identifier current-scope reference
    public const string EXPRESSION_PATTERN = $@"@@|@(?:{IDENTIFIER})?|{IDENTIFIER}";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string INTERPOLATION_PATTERN = $@"\$\{{(?<expression>{EXPRESSION_PATTERN})\}}";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string INDIRECTION_PATTERN = $@"\A&\{{(?<expression>{EXPRESSION_PATTERN})\}}\z";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string CAPTURE_PATTERN = $@"\A\*\{{(?<expression>{IDENTIFIER})\}}\z";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    // one or more hashes escape a single evaluation pass for interpolation, indirection, or capture
    public const string HASH_LITERAL_PATTERN = @"[$&*]\{(?<hashes>#+)(?<content>[^}]*)\}";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    // unescaped indirection or capture; a leading hash is an escape, not an active reference
    public const string ACTIVE_REFERENCE_PATTERN = @"[&*]\{(?!#)[^}]*\}";
}
