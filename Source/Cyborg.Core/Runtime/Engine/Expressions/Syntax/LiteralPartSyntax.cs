namespace Cyborg.Core.Runtime.Engine.Expressions.Syntax;

/// <summary>Literal text that contains no recognized expression token.</summary>
internal sealed record LiteralPartSyntax(string Text) : TextPartSyntax;
