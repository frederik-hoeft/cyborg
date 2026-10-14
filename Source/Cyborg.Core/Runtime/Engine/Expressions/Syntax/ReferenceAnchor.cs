namespace Cyborg.Core.Runtime.Engine.Expressions.Syntax;

/// <summary>The scope from which an expression resolves its reference.</summary>
internal enum ReferenceAnchor
{
    CurrentScope,
    EntryPoint
}
