using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Json.Configuration;
using Cyborg.Core.Aot.Json.Internals;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json.Serialization;

namespace Cyborg.Core.Aot.Json;

[Generator(LanguageNames.CSharp)]
public class GenericJsonTypeInfoGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(EmitFrameworkSource);

        IncrementalValuesProvider<Model> pipeline = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: typeof(JsonTypeInfoBindingsGeneratorAttribute).FullName,
            predicate: static (syntaxNode, _) => syntaxNode is ClassDeclarationSyntax,
            transform: static (context, _) =>
            {
                ISymbol targetClass = context.TargetSymbol;
                ImmutableArray<AttributeData> attributes = targetClass.GetAttributes();
                AttributeData genericJsonTypeInfoBindingsAttribute = attributes.FirstOrDefault(static attr => attr.AttributeClass is { } attributeClass && attributeClass.ToDisplayString() == typeof(JsonTypeInfoBindingsGeneratorAttribute).FullName)
                    ?? throw new InvalidOperationException($"{nameof(GenericJsonTypeInfoGenerator)} requires {nameof(JsonTypeInfoBindingsGeneratorAttribute)} to be applied to the class");
                ImmutableArray<AttributeData> jsonSerializables = 
                [
                    .. attributes.Where(static attr => attr.AttributeClass is {} attributeClass && attributeClass.ToDisplayString() == typeof(JsonSerializableAttribute).FullName)
                ];
                return new Model(
                    Namespace: targetClass.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)),
                    Class: targetClass,
                    GeneratorAttribute: genericJsonTypeInfoBindingsAttribute,
                    JsonSerializableAttributes: jsonSerializables);
            }
        );
        context.RegisterSourceOutput(pipeline, static (context, model) =>
        {
            JsonSerializableAttributeParser parser = new(context);
            string? overrideModifier = GetOptionalOverrideModifier(model);

            StringBuilder sourceBuilder = new(
                $$"""
                #nullable enable
  
                namespace {{model.Namespace}};
 
                partial class {{model.Class.Name}}
                {
                    public {{overrideModifier}}global::System.Text.Json.Serialization.Metadata.JsonTypeInfo<T>? GetTypeInfoOrDefault<T>()
                    {
                        // the JIT will optimize this switch statement away
                        return (object?)null switch
                        {

                """);

            string indent = new(' ', 3 * 4);
            // 1 = Optimized, 0 = Boxed Cast
            bool useFastTypeCast = model.GeneratorAttribute.NamedArguments.Any(static arg => arg is { Key: "GenerationMode", Value.Value: 1 });

            foreach (AttributeData jsonSerializable in model.JsonSerializableAttributes)
            {
                INamedTypeSymbol? type = parser.GetTargetType(jsonSerializable);
                if (type is null)
                {
                    continue;
                }
                sourceBuilder.Append(indent)
                    .Append($"_ when typeof(T) == typeof({type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}) => ");
                if (useFastTypeCast)
                {
                    sourceBuilder.AppendLine($"global::System.Runtime.CompilerServices.Unsafe.As<global::System.Text.Json.Serialization.Metadata.JsonTypeInfo<T>>({type.Name}),");
                }
                else
                {
                    sourceBuilder.AppendLine($"(global::System.Text.Json.Serialization.Metadata.JsonTypeInfo<T>)(object?){type.Name},");
                }
            }
            sourceBuilder.Append(
                """
                            _ => null,
                        };
                    }
                }
                """);

            SourceText sourceText = SourceText.From(sourceBuilder.ToString(), Encoding.UTF8);

            context.AddSource($"{model.Class.Name}.GenericJsonTypeInfoProvider.g.cs", sourceText);
        });
    }

    private static void EmitFrameworkSource(IncrementalGeneratorPostInitializationContext context)
    {
        context.AddEmbeddedSource<AotJsonSerializerContext>();
        context.AddEmbeddedSource<BindingsGenerationMode>();
        context.AddEmbeddedSource<IJsonTypeInfoProvider>();
        context.AddEmbeddedSource<JsonTypeInfoBindingsGeneratorAttribute>();
    }

    private static string? GetOptionalOverrideModifier(Model model)
    {
        // if the class inherits from AotJsonSerializerContext, then we need to generate an override for GetTypeInfoOrDefault<T>
        string? overrideModifier = null;

        // traverse the inheritance hierarchy to see if the class inherits from AotJsonSerializerContext
        for (INamedTypeSymbol? namedTypeSymbol = model.Class as INamedTypeSymbol; namedTypeSymbol is not null; namedTypeSymbol = namedTypeSymbol.BaseType)
        {
            if (namedTypeSymbol.ToDisplayString() == typeof(AotJsonSerializerContext).FullName)
            {
                // include the space after the override keyword
                overrideModifier = "override ";
                break;
            }
        }

        return overrideModifier;
    }

    private sealed record Model(string Namespace, ISymbol Class, AttributeData GeneratorAttribute, ImmutableArray<AttributeData> JsonSerializableAttributes);
}
