using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Cyborg.Core.Aot.Extensions;

internal static class BooleanExtensions
{
    extension(bool self)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool WithDefaults<T1>([MaybeNull] out T1? value)
        {
            value = default;
            return self;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool WithDefaults<T1, T2>([MaybeNull] out T1? value1, [MaybeNull] out T2? value2)
        {
            value1 = default;
            value2 = default;
            return self;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool WithDefaults<T1, T2, T3>([MaybeNull] out T1? value1, [MaybeNull] out T2? value2, [MaybeNull] out T3? value3)
        {
            value1 = default;
            value2 = default;
            value3 = default;
            return self;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool WithDefaults<T1, T2, T3, T4>([MaybeNull] out T1? value1, [MaybeNull] out T2? value2, [MaybeNull] out T3? value3, [MaybeNull] out T4? value4)
        {
            value1 = default;
            value2 = default;
            value3 = default;
            value4 = default;
            return self;
        }
    }
}
