using Cyborg.Core.Runtime.Engine.Expressions.Bound;

namespace Cyborg.Core.Runtime.Engine.Expressions.Syntax;

/// <summary>Source-order text segment with polymorphic binding into a distinct bound node.</summary>
internal abstract record TextPartSyntax
{
    public abstract BoundTextPart Bind();
}
