using System.Collections.Immutable;

namespace Cyborg.Core.Common.Extensions;

internal static class ImmutableArrayExtensions
{
    public static ImmutableArray<T> OrEmpty<T>(this ImmutableArray<T> self)
        => self.IsDefault ? [] : self;
}
