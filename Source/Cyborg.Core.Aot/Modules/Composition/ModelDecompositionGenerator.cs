using Cyborg.Core.Aot.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;

namespace Cyborg.Core.Aot.Modules.Composition;

[Generator(LanguageNames.CSharp)]
public class ModelDecompositionGenerator : IIncrementalGenerator
{
    private const string I_DECOMPOSABLE_INTERFACE_FULL_NAME = "Cyborg.Core.Modules.Configuration.Model.IDecomposable";
    private const string DYNAMIC_KEY_VALUE_PAIR_FULL_NAME = "Cyborg.Core.Modules.Configuration.Model.DynamicKeyValuePair";
    private const string DECOMPOSE_METHOD_NAME = "Decompose";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(EmitFrameworkSource);

        IncrementalValuesProvider<Model> pipeline = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: typeof(GeneratedDecompositionAttribute).FullName,
            predicate: static (syntaxNode, _) => syntaxNode is RecordDeclarationSyntax or ClassDeclarationSyntax,
            transform: static (context, _) =>
            {
                ISymbol targetClass = context.TargetSymbol;
                ImmutableArray<AttributeData> attributes = targetClass.GetAttributes();
                AttributeData generatedDecompositionAttribute = attributes.FirstOrDefault(static attr => 
                    attr.AttributeClass is { } attributeClass && attributeClass.ToDisplayString() == typeof(GeneratedDecompositionAttribute).FullName)
                    ?? throw new InvalidOperationException($"{nameof(ModelDecompositionGenerator)} requires {nameof(GeneratedDecompositionAttribute)} to be applied to the class");
                if (targetClass is not INamedTypeSymbol namedTypeSymbol)
                {
                    throw new InvalidOperationException($"{nameof(ModelDecompositionGenerator)} can only be applied to classes");
                }
                // enumerate all public properties of the class that are not decorated with the [DecomposeIgnore] attribute
                ImmutableArray<IPropertySymbol> decomposableProperties = 
                [
                    .. namedTypeSymbol.GetMembers()
                    .OfType<IPropertySymbol>()
                    .Where(property => property.DeclaredAccessibility is Accessibility.Public
                        && !property.GetAttributes().Any(attr => 
                            attr.AttributeClass is { } attributeClass 
                            && attributeClass.ToDisplayString() == typeof(DecomposeIgnoreAttribute).FullName))
                ];

                return new Model(
                    Namespace: targetClass.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)),
                    Class: namedTypeSymbol,
                    GeneratorAttribute: generatedDecompositionAttribute,
                    decomposableProperties);
            }
        );
        context.RegisterSourceOutput(pipeline, static (context, model) =>
        {
            // get the naming policy from the attribute, if specified (type + static property name) and apply it to the property names
            string namingPolicyProviderFullName = (model.GeneratorAttribute.NamedArguments.FirstOrDefault(kv => kv.Key == nameof(GeneratedDecompositionAttribute.NamingPolicyProvider)).Value.Value as INamedTypeSymbol)
                ?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Included))
                ?? $"global::{typeof(JsonNamingPolicy).FullName}";
            string namingPolicyStaticPropertyName = model.GeneratorAttribute.NamedArguments.FirstOrDefault(kv => kv.Key == nameof(GeneratedDecompositionAttribute.NamingPolicy)).Value.Value as string
                ?? nameof(JsonNamingPolicy.SnakeCaseLower);
            StringBuilder sourceBuilder = new(
                $$"""
                #nullable enable

                namespace {{model.Namespace}};

                partial record {{model.Class.Name}} : global::{{I_DECOMPOSABLE_INTERFACE_FULL_NAME}}
                {
                    public global::{{typeof(IEnumerable<>).Namespace}}.{{nameof(IEnumerable<>)}}<global::{{DYNAMIC_KEY_VALUE_PAIR_FULL_NAME}}> {{DECOMPOSE_METHOD_NAME}}() =>
                    [

                """);
            foreach (IPropertySymbol property in model.DecomposableProperties)
            {
                sourceBuilder.AppendLine(
                    $$"""
                            new({{namingPolicyProviderFullName}}.{{namingPolicyStaticPropertyName}}.{{nameof(JsonNamingPolicy.ConvertName)}}(nameof({{property.Name}})), {{property.Name}}),
                    """);
            }
            sourceBuilder.Append(
                """
                    ];
                }
                """);

            SourceText sourceText = SourceText.From(sourceBuilder.ToString(), Encoding.UTF8);

            context.AddSource($"{model.Class.Name}.Decomposition.g.cs", sourceText);
        });
    }

    private static void EmitFrameworkSource(IncrementalGeneratorPostInitializationContext context)
    {
        context.AddEmbeddedSource<DecomposeIgnoreAttribute>();
        context.AddEmbeddedSource<GeneratedDecompositionAttribute>();
    }

    private sealed record Model
    (
        string Namespace,
        INamedTypeSymbol Class,
        AttributeData GeneratorAttribute,
        ImmutableArray<IPropertySymbol> DecomposableProperties
    );
}
