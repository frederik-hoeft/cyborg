using Cyborg.Core.Aot.Contracts;
using Cyborg.Core.Parsing.SyntaxNodes;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Parsing.Parsers;

[GeneratorContractRegistration<ModuleValidationGeneratorContract>(ModuleValidationGeneratorContract.IParser)]
public interface IParser
{
    string? Name { get; }

    IParser NamedCopy(string name);

    bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed);
}
