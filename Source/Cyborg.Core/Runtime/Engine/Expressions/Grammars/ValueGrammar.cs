using Cyborg.Core.Parsing;
using Cyborg.Core.Parsing.Parsers;
using Cyborg.Core.Parsing.SyntaxNodes;
using Cyborg.Core.Runtime.Engine.Expressions.Parsers;
using Cyborg.Core.Runtime.Engine.Expressions.Syntax;
using Cyborg.Core.Runtime.Engine.Expressions.SyntaxNodes;
using Cyborg.Core.Runtime.Engine.Expressions.Visitors;
using Cyborg.Core.Text;
using System.Collections.Immutable;

namespace Cyborg.Core.Runtime.Engine.Expressions.Grammars;

/// <summary>
/// Compiles the existing variable value language into immutable syntax, without binding to an environment or evaluating references.
/// </summary>
internal sealed class ValueGrammar
{
    private readonly IParser _typed = Grammar.Alternative(new TypedReferenceParser(), new CaptureParser());
    private readonly IParser _text;

    private ValueGrammar()
    {
        IParser tokens = Grammar.Alternative(new InterpolationParser(), new EscapedPartParser(), new ActiveTypedFragmentParser());
        _text = Grammar.Repeat(Grammar.Alternative(tokens, new LiteralRunParser(tokens)));
    }

    public static ValueGrammar Instance { get; } = new();

    /// <summary>Converts arbitrary environment values to immutable syntax, without interpreting non-text CLR objects.</summary>
    public ValueSyntax Compile(object? value) => value switch
    {
        string text => Parse(text),
        TaggedString tagged => Parse(tagged),
        _ => new TerminalValueSyntax(value)
    };

    /// <summary>Compiles tagged textual input without discarding tags or its CLR text kind.</summary>
    public ValueSyntax Parse(TaggedString input) => new TaggedValueSyntax(Parse(input.Value), input.Tags);

    public ValueSyntax Parse(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (_typed.TryParseComplete(input, out ISyntaxNode? typed) && typed is ValueNode value)
        {
            return value.Evaluate();
        }
        return ParseText(input);
    }

    /// <summary>Parses text-only contexts, such as keys, where typed indirection/capture is invalid.</summary>
    public TextValueSyntax ParseText(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!_text.TryParseComplete(input, out ISyntaxNode? root))
        {
            throw new FormatException($"Invalid textual variable expression '{input}'.");
        }
        TextSyntaxCollector collector = new();
        root.Accept(collector);
        return new TextValueSyntax(ImmutableArray.CreateRange(collector.Parts));
    }
}
