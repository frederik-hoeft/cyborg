using Cyborg.Core.Parsing.Parsers;

namespace Cyborg.Core.Parsing.Grammars.Builders;

public sealed class GrammarAlternativeBuilder : GrammarCollectionBuilder
{
    public GrammarAlternativeBuilder Sequence(Action<GrammarSequenceBuilder> buildSequence) => AddSequence(this, buildSequence);

    public GrammarAlternativeBuilder Optional(Action<GrammarOptionalBuilder> buildOptional) => AddOptional(this, buildOptional);

    public GrammarAlternativeBuilder Alternative(Action<GrammarAlternativeBuilder> buildAlternative) => AddAlternative(this, buildAlternative);

    public GrammarAlternativeBuilder Parser(IParser parser) => AddParser(this, parser);

    public GrammarAlternativeBuilder Parser<TParser>() where TParser : class, IParser<TParser> => AddParser(this, TParser.Instance);

    public override IParser Build() => new Alternative([.. GetChildrenNonEmpty()]);
}
