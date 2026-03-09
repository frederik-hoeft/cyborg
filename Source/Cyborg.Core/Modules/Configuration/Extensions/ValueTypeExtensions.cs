using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Modules.Configuration.Extensions;

[SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "False positive for C# 14 extension classes.")]
public static class ValueTypeExtensions
{
    extension<T>(T value) where T : unmanaged
    {
        public T OnDefault(T defaultValue, T when = default)
        {
            if (value.Equals(when))
            {
                return defaultValue;
            }
            return value;
        }
    }
}
