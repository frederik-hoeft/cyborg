using Cyborg.Core.Parsing.SyntaxNodes;

namespace Cyborg.Modules.Borg.Create.InputValidation;

internal sealed class NumberSyntaxNode(string? name, int value) : ValidationSyntaxNode(name, value.ToString());
