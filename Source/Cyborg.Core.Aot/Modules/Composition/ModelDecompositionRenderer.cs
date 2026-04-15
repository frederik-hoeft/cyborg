using Cyborg.Core.Aot.Extensions;
using Microsoft.CodeAnalysis;
using System.Text;

namespace Cyborg.Core.Aot.Modules.Composition;

internal static class ModelDecompositionRenderer
{
    private const string DECOMPOSE_METHOD_NAME = "Decompose";

    private static readonly SymbolDisplayFormat s_fullyQualifiedFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Included);

    public static string Render(DecompositionGenerationModel model, DecompositionContractInfo contractInfo)
    {
        StringBuilder sourceBuilder = new(
            $$"""
            #nullable enable

            {{RenderNamespace(model.Namespace)}}
            partial {{model.TypeKeyword}} {{model.TypeSymbol.Name}} : {{contractInfo.IDecomposable.ToDisplayString(s_fullyQualifiedFormat)}}
            {
                public {{KnownTypes.IEnumerableOfT(contractInfo.DynamicKeyValuePair.ToDisplayString(s_fullyQualifiedFormat))}} {{DECOMPOSE_METHOD_NAME}}() =>
                [

            """);

        IndentedStringBuilder indentedBuilder = new(sourceBuilder, indentLevel: 2);
        foreach (IPropertySymbol property in model.DecomposableProperties)
        {
            indentedBuilder.AppendLine($"new({model.NamingPolicyProviderTypeName}.{model.NamingPolicyPropertyName}.ConvertName(nameof({property.Name})), {property.Name}),");
        }

        sourceBuilder.Append(
            """
                ];
            }
            """);

        return sourceBuilder.ToString();
    }

    private static string RenderNamespace(string namespaceName) =>
        string.IsNullOrWhiteSpace(namespaceName)
            ? string.Empty
            : $"""
              namespace {namespaceName};


              """;

}
