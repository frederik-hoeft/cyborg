using System.Collections.Immutable;

namespace Cyborg.Core.Common.Extensions;

[SuppressMessage("Design", CA1034, Justification = CA1034_JUSTIFY_EXTENSION_SYNTAX_CSHARP_14)]
public static class ImmutableArrayExtensions
{
    extension<T>(ImmutableArray<T> self)
    {
        public ImmutableArray<T> OrEmpty() => self.IsDefault ? [] : self;
    }
}
