using Cyborg.Core.Runtime.Engine.Expressions.Bound;
using System.Collections.Immutable;

namespace Cyborg.Core.Runtime.Engine.Expressions.Syntax;

/// <summary>Source text kind and tags are part of the syntax wrapper, independent of the expression kind.</summary>
internal sealed record TaggedValueSyntax(ValueSyntax Inner, ImmutableHashSet<string> Tags) : ValueSyntax
{
    public override BoundValue Bind(IExpressionResolutionContext context) =>
        new BoundWithTags(Inner.Bind(context), Tags, PreserveTaggedTextKind: Inner is TextValueSyntax);
}
