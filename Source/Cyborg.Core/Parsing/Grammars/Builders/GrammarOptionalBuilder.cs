using Cyborg.Core.Parsing.Parsers;

namespace Cyborg.Core.Parsing.Grammars.Builders;

public sealed class GrammarOptionalBuilder : GrammarBuilder
{
    private IParser? _childParser;

    public void Sequence(Action<GrammarSequenceBuilder> buildSequence) => SelfIfClean()._childParser = CreateSequence(buildSequence);

    public void Alternative(Action<GrammarAlternativeBuilder> buildAlternative) => SelfIfClean()._childParser = CreateAlternative(buildAlternative);

    public void Parser(IParser parser) => SelfIfClean()._childParser = parser;

    public void Parser<TParser>() where TParser : class, IParser<TParser> => SelfIfClean()._childParser = TParser.Instance;

    private GrammarOptionalBuilder SelfIfClean()
    {
        if (_childParser is not null)
        {
            throw new InvalidOperationException("Child parser has already been set.");
        }
        return this;
    }

    public override IParser Build()
    {
        if (_childParser is null)
        {
            throw new InvalidOperationException("Child parser has not been set.");
        }
        return new Optional(_childParser);
    }
}
