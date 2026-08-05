using System.Text;

namespace Cyborg.Core.Aot.Extensions;

internal static class TypeExtensions
{
    private const string GLOBAL = "global::";

    extension(Type self)
    {
        public string RenderGlobalWithGenerics(params IReadOnlyList<string> genericArguments)
        {
            if (!self.IsGenericTypeDefinition)
            {
                throw new InvalidOperationException($"Type '{self.FullName}' is not a generic type definition.");
            }
            StringBuilder builder = new();
            // strip any generic arity suffix from the type name (e.g., `List`1` becomes `List`)
            builder.Append(GLOBAL).Append(self.Namespace).Append('.').Append(self.Name.Split('`')[0]);
            if (genericArguments.Count > 0)
            {
                builder.Append('<');
                for (int i = 0; i < genericArguments.Count; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(", ");
                    }
                    builder.Append(genericArguments[i]);
                }
                builder.Append('>');
            }
            return builder.ToString();
        }

        public string RenderGlobalWithGenerics(params IReadOnlyList<Type> genericArguments) =>
            self.RenderGlobalWithGenerics(genericArguments.Select(t => t.RenderGlobal()).ToList());

        public string RenderGlobal() => $"{GLOBAL}{self.FullName}";
    }
}
