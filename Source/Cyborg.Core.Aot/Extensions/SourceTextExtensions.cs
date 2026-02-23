using Microsoft.CodeAnalysis.Text;
using System.Text;

namespace Cyborg.Core.Aot.Extensions;

/// <summary>
/// Helper to load embedded source text from this assembly.
/// </summary>
internal static class SourceTextExtensions
{
    extension(SourceText)
    {
        public static SourceText FromEmbedded<T>()
        {
            if (typeof(T).Assembly != typeof(SourceTextExtensions).Assembly)
            {
                throw new InvalidOperationException("The type T must be defined in the same assembly as SourceTextExtensions.");
            }
            string typeName = typeof(T).FullName ?? throw new InvalidOperationException("The type T must have a full name.");
            using Stream stream = typeof(SourceTextExtensions).Assembly.GetManifestResourceStream($"{typeName}.cs")
                ?? throw new InvalidOperationException($"The embedded resource '{typeName}.cs' was not found.");
            return SourceText.From(stream, Encoding.UTF8, canBeEmbedded: true);
        }

        public static SourceText FromEmbedded<T>(string typeName)
        {
            using Stream stream = typeof(SourceTextExtensions).Assembly.GetManifestResourceStream($"{typeof(T).Namespace}.{typeName}.cs")
                ?? throw new InvalidOperationException($"The embedded resource '{typeof(T).Namespace}.{typeName}.cs' was not found.");
            return SourceText.From(stream, Encoding.UTF8, canBeEmbedded: true);
        }
    }
}
