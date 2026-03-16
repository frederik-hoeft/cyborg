using Cyborg.Core.Parsing.SyntaxNodes;

namespace Cyborg.Modules.Borg.Create.InputValidation;

internal sealed class LiteralSyntaxNode(string? name, string value) : ValidationSyntaxNode(name, value);