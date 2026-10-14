namespace Cyborg.Core.Runtime.Engine.Expressions.Syntax;

/// <summary>
/// An escaped operator expression. Escape depth and original body remain structural; no escape layer is consumed during parsing.
/// </summary>
internal sealed record EscapedPartSyntax(char Operator, int EscapeDepth, string Body) : TextPartSyntax;
