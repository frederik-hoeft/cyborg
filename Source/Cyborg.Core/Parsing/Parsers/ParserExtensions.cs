using Cyborg.Core.Aot.Contracts;
using Cyborg.Core.Parsing.SyntaxNodes;
using Cyborg.Core.Parsing.Visitors;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Parsing.Parsers;

[GeneratorContractRegistration<ModuleValidationGeneratorContract>(ModuleValidationGeneratorContract.ParserExtensions)]
public static class ParserExtensions
{
    /// <summary>
    /// Parses an entire input, rejecting otherwise valid prefixes with trailing characters.
    /// </summary>
    public static bool TryParseComplete(this IParser parser, string input, [NotNullWhen(true)] out ISyntaxNode? syntaxNode)
    {
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(input);
        if (parser.TryParse(input, 0, out syntaxNode, out int charsConsumed) && charsConsumed == input.Length)
        {
            return true;
        }
        syntaxNode = null;
        return false;
    }

    public static bool CanAccept<TVisitor>(this IParser parser)
        where TVisitor : class, INodeVisitor
    {
        ArgumentNullException.ThrowIfNull(parser);
        return parser.CanAccept(typeof(TVisitor));
    }

    public static IParser RequireVisitor<TVisitor>(this IParser parser)
        where TVisitor : class, INodeVisitor
    {
        ArgumentNullException.ThrowIfNull(parser);
        if (!parser.CanAccept<TVisitor>())
        {
            throw new InvalidOperationException($"Parser '{parser.Name ?? parser.GetType().Name}' does not support visitor '{typeof(TVisitor).FullName}'.");
        }
        return parser;
    }
}
