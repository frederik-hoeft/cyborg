using System.Text;

namespace Cyborg.Core.Aot.Extensions;

internal static class TypeExtensions
{
    extension(Type self)
    {
        public string ConstructFullyQualifiedGenericName(params ReadOnlySpan<Type> genericArguments)
        {
            if (!self.IsGenericTypeDefinition)
            {
                throw new InvalidOperationException("The type must be a generic type definition (i.e., it must have unassigned generic parameters).");
            }
            StringBuilder builder = new();
            builder.Append("global::").Append(self.Namespace).Append('.').Append(self.Name);
            if (genericArguments.Length > 0)
            {
                builder.Append('<');
                for (int i = 0; i < genericArguments.Length; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(", ");
                    }
                    builder.Append(genericArguments[i].ConstructFullyQualifiedGenericName());
                }
                builder.Append('>');
            }
            return builder.ToString();
        }
    }
}
