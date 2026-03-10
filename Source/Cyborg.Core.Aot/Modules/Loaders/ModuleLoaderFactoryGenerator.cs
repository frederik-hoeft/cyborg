using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;

namespace Cyborg.Core.Aot.Modules.Loaders;

[Generator(LanguageNames.CSharp)]
public class ModuleLoaderFactoryGenerator : IIncrementalGenerator
{
    private const string I_MODULE_WORKER_INTERFACE_FULL_NAME = "Cyborg.Core.Modules.IModuleWorker";
    private const string MODULE_LOADER_UNBOUND = "Cyborg.Core.Modules.Configuration.ModuleLoader<,>";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(EmitFrameworkSource);

        IncrementalValuesProvider<Model> pipeline = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: typeof(GeneratedModuleLoaderFactoryAttribute).FullName,
            predicate: static (syntaxNode, _) => syntaxNode is ClassDeclarationSyntax,
            transform: static (context, _) =>
            {
                ISymbol targetClass = context.TargetSymbol;
                ImmutableArray<AttributeData> attributes = targetClass.GetAttributes();
                AttributeData generatedModuleLoaderFactoryAttribute = attributes.FirstOrDefault(static attr => 
                    attr.AttributeClass is { } attributeClass && attributeClass.ToDisplayString() == typeof(GeneratedModuleLoaderFactoryAttribute).FullName)
                    ?? throw new InvalidOperationException($"{nameof(ModuleLoaderFactoryGenerator)} requires {nameof(GeneratedModuleLoaderFactoryAttribute)} to be applied to the class");
                // The base type must be ModuleLoader<,> or a derived type
                // We also need to find the first type argument passed to ModuleLoader<TModuleWorker, _>  that must also implement IModuleWorker, so we can use it to generate the correct return type for GetTypeInfoOrDefault<T>
                for (ISymbol? symbol = targetClass; symbol is INamedTypeSymbol namedTypeSymbol; symbol = namedTypeSymbol.BaseType)
                {
                    if (!namedTypeSymbol.IsGenericType
                        || namedTypeSymbol.TypeArguments is not ([ITypeSymbol moduleWorkerType, ITypeSymbol moduleType])
                        || namedTypeSymbol.ConstructUnboundGenericType().ToDisplayString() != MODULE_LOADER_UNBOUND)
                    {
                        continue;
                    }
                    if (!moduleWorkerType.AllInterfaces.Any(i => i.ToDisplayString() == I_MODULE_WORKER_INTERFACE_FULL_NAME))
                    {
                        throw new InvalidOperationException($"{nameof(ModuleLoaderFactoryGenerator)} requires the first type argument of {MODULE_LOADER_UNBOUND} to implement {I_MODULE_WORKER_INTERFACE_FULL_NAME}");
                    }
                    // also need to get the primary constructor of the module worker for DI generation
                    if (moduleWorkerType is not INamedTypeSymbol targetNamedTypeSymbol || targetNamedTypeSymbol.InstanceConstructors is not [IMethodSymbol constructor])
                    {
                        throw new InvalidOperationException($"{nameof(ModuleLoaderFactoryGenerator)} requires the target class to have a single constructor");
                    }
                    string? methodName = null;
                    string? methodAccessibility = null;
                    TypedConstant nameArg = generatedModuleLoaderFactoryAttribute.NamedArguments
                        .FirstOrDefault(static kvp => kvp.Key == nameof(GeneratedModuleLoaderFactoryAttribute.Name)).Value;
                    if (nameArg.Value is string name && !string.IsNullOrEmpty(name))
                    {
                        methodName = name;
                        if (targetClass is not INamedTypeSymbol targetClassNamedType)
                        {
                            throw new InvalidOperationException($"{nameof(ModuleLoaderFactoryGenerator)} requires the target class to be a named type when Name is specified");
                        }
                        IMethodSymbol partialMethod = targetClassNamedType.GetMembers(name)
                            .OfType<IMethodSymbol>()
                            .FirstOrDefault(static m => m.IsPartialDefinition) 
                            ?? throw new InvalidOperationException($"{nameof(ModuleLoaderFactoryGenerator)} requires a partial method declaration named '{name}' in class '{targetClass.Name}'");

                        SymbolDisplayFormat qualifiedFormat = SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Included);
                        if (partialMethod.ReturnType.ToDisplayString(qualifiedFormat) != moduleWorkerType.ToDisplayString(qualifiedFormat))
                        {
                            throw new InvalidOperationException($"{nameof(ModuleLoaderFactoryGenerator)} requires the partial method '{name}' to return '{moduleWorkerType.ToDisplayString()}'");
                        }
                        if (partialMethod.Parameters is not [IParameterSymbol param1, IParameterSymbol param2]
                            || param1.Type.ToDisplayString(qualifiedFormat) != moduleType.ToDisplayString(qualifiedFormat)
                            || param2.Type.ToDisplayString(qualifiedFormat) != KnownTypes.IServiceProvider)
                        {
                            throw new InvalidOperationException($"{nameof(ModuleLoaderFactoryGenerator)} requires the partial method '{name}' to have parameters '({moduleType.ToDisplayString()} module, {typeof(IServiceProvider).FullName} serviceProvider)'");
                        }
                        methodAccessibility = partialMethod.DeclaredAccessibility switch
                        {
                            Accessibility.Public => "public",
                            Accessibility.Protected => "protected",
                            Accessibility.Internal => "internal",
                            Accessibility.ProtectedOrInternal => "protected internal",
                            Accessibility.ProtectedAndInternal => "private protected",
                            Accessibility.Private => "private",
                            _ => throw new InvalidOperationException($"{nameof(ModuleLoaderFactoryGenerator)} could not determine accessibility of partial method '{name}'")
                        };
                    }
                    return new Model(
                        Namespace: targetClass.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)),
                        Class: targetClass,
                        GeneratorAttribute: generatedModuleLoaderFactoryAttribute,
                        Constructor: constructor,
                        ModuleWorkerType: moduleWorkerType,
                        ModuleType: moduleType,
                        MethodName: methodName,
                        MethodAccessibility: methodAccessibility);
                }
                throw new InvalidOperationException($"{nameof(ModuleLoaderFactoryGenerator)} requires the target class to inherit from {MODULE_LOADER_UNBOUND}");
            }
        );
        IncrementalValueProvider<(Compilation, ImmutableArray<Model>)> compilationAndPipeline =
            context.CompilationProvider.Combine(pipeline.Collect());
        context.RegisterSourceOutput(compilationAndPipeline, static (context, compilationModel) =>
        {
            (Compilation compilation, ImmutableArray<Model> models) = compilationModel;
            foreach (Model model in models)
            {
                string methodModifiers = model.MethodName is not null
                    ? $"{model.MethodAccessibility} partial "
                    : "protected override ";
                string methodName = model.MethodName ?? "CreateWorker";
                StringBuilder sourceBuilder = new(
                    $$"""
                    #nullable enable
                    using global::Microsoft.Extensions.DependencyInjection;

                    namespace {{model.Namespace}};

                    partial class {{model.Class.Name}}
                    {
                        {{methodModifiers}}{{model.ModuleWorkerType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Included))}} {{methodName}}(
                            {{model.ModuleType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Included))}} module, 
                            {{KnownTypes.IServiceProvider}} serviceProvider)
                        {
                            return new {{model.ModuleWorkerType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Included))}}(
                    """);
                for (int i = 0; i < model.Constructor.Parameters.Length; ++i)
                {
                    IParameterSymbol parameter = model.Constructor.Parameters[i];
                    if (i == 0)
                    {
                        sourceBuilder.AppendLine();
                    }
                    else
                    {
                        sourceBuilder.AppendLine(",");
                    }
                    // check if the parameter is the module type, if so pass the module parameter, otherwise resolve from the service provider
                    if (parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Included)) 
                        == model.ModuleType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Included)))
                    {
                        sourceBuilder.Append(
                            """
                                        module
                            """);
                    }
                    else
                    {
                        // using serviceProvider.GetRequiredService to resolve dependencies, this will throw an exception if the service is not registered, which is fine because it will be a configuration error that should be fixed by the user
                        sourceBuilder.Append(
                            $"""
                                        serviceProvider.GetRequiredService<{parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Included))}>()
                            """);
                    }
                }
                sourceBuilder.Append(
                    """
                    );
                        }
                    }
                    """);

                SourceText sourceText = SourceText.From(sourceBuilder.ToString(), Encoding.UTF8);

                context.AddSource($"{model.Class.Name}.ModuleLoaderFactory.g.cs", sourceText);
            }
        });
    }

    private static void EmitFrameworkSource(IncrementalGeneratorPostInitializationContext context) => 
        context.AddEmbeddedSource<GeneratedModuleLoaderFactoryAttribute>();

    private sealed record Model(string Namespace, ISymbol Class, AttributeData GeneratorAttribute, IMethodSymbol Constructor, ITypeSymbol ModuleWorkerType, ITypeSymbol ModuleType, string? MethodName, string? MethodAccessibility);
}
