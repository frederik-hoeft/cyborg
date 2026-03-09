namespace Cyborg.Core.Modules.Configuration.Extensions;

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
