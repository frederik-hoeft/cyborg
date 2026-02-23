using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Cyborg.Core.Aot.Extensions;

/// <summary>
/// Helper to emit embedded source files from this generator assembly into the compilation.
/// </summary>
internal static class IncrementalGeneratorPostInitializationContextExtensions
{
    extension(IncrementalGeneratorPostInitializationContext self)
    {
        public void AddEmbeddedSource<T>()
        {
            SourceText sourceText = SourceTextExtensions.FromEmbedded<T>();
            self.AddSource($"{typeof(T).FullName}.cs", sourceText);
        }

        public void AddEmbeddedSource<T>(string typeName)
        {
            SourceText sourceText = SourceTextExtensions.FromEmbedded<T>(typeName);
            self.AddSource($"{typeof(T).Namespace}.{typeName}.cs", sourceText);
        }
    }
}