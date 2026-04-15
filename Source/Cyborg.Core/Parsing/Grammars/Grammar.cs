using Cyborg.Core.Parsing.Grammars.Builders;
using Cyborg.Core.Parsing.Parsers;

namespace Cyborg.Core.Parsing.Grammars;

public sealed class Grammar : GrammarBuilder
{
    public override IParser Build() => throw new NotSupportedException();

    public static IParser Sequence(Action<GrammarSequenceBuilder> buildSequence) => CreateSequence(buildSequence);

    public static IParser Sequence(params ReadOnlySpan<IParser> parsers) => CreateSequence(parsers);

    public static IParser Alternative(Action<GrammarAlternativeBuilder> buildAlternative) => CreateAlternative(buildAlternative);

    public static IParser Alternative(params ReadOnlySpan<IParser> parsers) => CreateAlternative(parsers);

    public static IParser Optional(Action<GrammarOptionalBuilder> buildOptional) => CreateOptional(buildOptional);

    public static IParser Optional(IParser parser) => CreateOptional(parser);

    public static IParser Parser<T>() where T : class, IParser<T> => T.Instance;
}
