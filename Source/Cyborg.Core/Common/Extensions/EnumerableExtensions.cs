namespace Cyborg.Core.Common.Extensions;

[SuppressMessage("Design", CA1034, Justification = CA1034_JUSTIFY_EXTENSION_SYNTAX_CSHARP_14)]
public static class EnumerableExtensions
{
    extension<T>(IEnumerable<T> self)
    {
        public void ForEach(Action<T> action)
        {
            ArgumentNullException.ThrowIfNull(action);

            foreach (T item in self)
            {
                action(item);
            }
        }
    }
}
