using Cyborg.Core.Aot.Contracts;
using Cyborg.Core.Parsing.SyntaxNodes;

namespace Cyborg.Core.Parsing.Parsers;

[GeneratorContractRegistration<ModuleValidationGeneratorContract>(ModuleValidationGeneratorContract.IParser)]
public interface IParser
{
    string? Name { get; }

    IParser NamedCopy(string name);

    bool TryParse(ReadOnlySpan<char> input, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed);
}
