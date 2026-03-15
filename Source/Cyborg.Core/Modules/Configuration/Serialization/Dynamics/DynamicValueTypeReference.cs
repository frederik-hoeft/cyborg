using System.Collections.Immutable;
using System.Text;

namespace Cyborg.Core.Modules.Configuration.Serialization.Dynamics;

public sealed record DynamicValueTypeReference(string TypeName, ImmutableArray<DynamicValueTypeReference> TypeArguments)
{
    public bool IsGeneric => !TypeArguments.IsDefaultOrEmpty;

    public override string ToString()
    {
        if (!IsGeneric)
        {
            return TypeName;
        }

        StringBuilder builder = new();
        builder.Append(TypeName);
        builder.Append('<');

        for (int i = 0; i < TypeArguments.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append(TypeArguments[i]);
        }

        builder.Append('>');
        return builder.ToString();
    }
}