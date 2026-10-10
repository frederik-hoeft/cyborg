using Cyborg.Core.Parsing.SyntaxNodes;

namespace Cyborg.Modules.Borg.Create.InputValidation;

internal sealed class NumberSyntaxNode(string? name, int value) : ResultSyntaxNodeBase<string>(name, value.ToString());
