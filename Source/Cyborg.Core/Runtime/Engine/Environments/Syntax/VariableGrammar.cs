namespace Cyborg.Core.Runtime.Engine.Environments.Syntax;

/// <summary>
/// Defines the grammar for variable identifiers, namespaces, and interpolations used in the environment.
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
    // Longest suffix first so []+ is not consumed as [].
    public const string COLLECTION_SUFFIX = @"(?:\[\]\+|\[\+\]|\[\])";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string COLLECTION_ACCESS_PATTERN = $@"\A(?<name>{IDENTIFIER})(?<suffix>{COLLECTION_SUFFIX})\z";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    // allow ${@@} for late self references, ${@} for self references, ${@identifier} for late refs, and ${identifier} for normal references.
    // A reference may also carry a virtual-collection suffix ([] , [+], or []+).
    public const string INTERPOLATION_PATTERN = $@"\$\{{(?<expression>@@|@(?:{IDENTIFIER}{COLLECTION_SUFFIX}?)?|{IDENTIFIER}{COLLECTION_SUFFIX}?)\}}";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string HASH_LITERAL_PATTERN = @"\$\{(?<hashes>#+)(?<content>[^}]*)\}";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string INDIRECTION_PATTERN = $@"\A{INTERPOLATION_PATTERN}\z";
}
