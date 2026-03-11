using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Cyborg.Core.Aot.Modules.Loaders;

internal static class LoaderGenerationCandidateFactory
{
    private static readonly SymbolDisplayFormat s_fullyQualifiedFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Included);

    public static LoaderGenerationCandidate Create(LoaderAnnotatedTarget target, LoaderContractInfo contractInfo)
    {
        ImmutableArray<Diagnostic>.Builder diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        INamedTypeSymbol typeSymbol = target.TypeSymbol;

        if (typeSymbol.TypeKind is not TypeKind.Class)
        {
            diagnostics.Add(Diagnostic.Create(
                ModuleLoaderFactoryGeneratorDiagnostics.TargetMustBeClass,
                typeSymbol.Locations.FirstOrDefault(),
                typeSymbol.Name));

            return new LoaderGenerationCandidate(null, diagnostics.ToImmutable());
        }

        INamedTypeSymbol? moduleLoaderBase = FindModuleLoaderBase(typeSymbol, contractInfo.ModuleLoaderT);
        if (moduleLoaderBase is null)
        {
            diagnostics.Add(Diagnostic.Create(
                ModuleLoaderFactoryGeneratorDiagnostics.TargetMustInheritModuleLoader,
                typeSymbol.Locations.FirstOrDefault(),
                typeSymbol.Name,
                contractInfo.ModuleLoaderT.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));

            return new LoaderGenerationCandidate(null, diagnostics.ToImmutable());
        }

        if (moduleLoaderBase.TypeArguments is not [ITypeSymbol moduleWorkerTypeSymbol, ITypeSymbol moduleType])
        {
            diagnostics.Add(Diagnostic.Create(
                ModuleLoaderFactoryGeneratorDiagnostics.UnexpectedModuleLoaderShape,
                typeSymbol.Locations.FirstOrDefault(),
                typeSymbol.Name,
                moduleLoaderBase.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));

            return new LoaderGenerationCandidate(null, diagnostics.ToImmutable());
        }

        if (moduleWorkerTypeSymbol is not INamedTypeSymbol moduleWorkerType)
        {
            diagnostics.Add(Diagnostic.Create(
                ModuleLoaderFactoryGeneratorDiagnostics.ModuleWorkerTypeMustBeNamedType,
                typeSymbol.Locations.FirstOrDefault(),
                moduleWorkerTypeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                typeSymbol.Name));

            return new LoaderGenerationCandidate(null, diagnostics.ToImmutable());
        }

        if (!ImplementsInterface(moduleWorkerType, contractInfo.IModuleWorker))
        {
            diagnostics.Add(Diagnostic.Create(
                ModuleLoaderFactoryGeneratorDiagnostics.ModuleWorkerMustImplementInterface,
                typeSymbol.Locations.FirstOrDefault(),
                moduleWorkerType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                contractInfo.IModuleWorker.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));

            return new LoaderGenerationCandidate(null, diagnostics.ToImmutable());
        }

        ImmutableArray<IMethodSymbol> declaredConstructors = [.. moduleWorkerType.InstanceConstructors.Where(static ctor => !ctor.IsImplicitlyDeclared)];

        if (declaredConstructors.Length != 1)
        {
            diagnostics.Add(Diagnostic.Create(
                ModuleLoaderFactoryGeneratorDiagnostics.ModuleWorkerMustHaveSingleConstructor,
                moduleWorkerType.Locations.FirstOrDefault() ?? typeSymbol.Locations.FirstOrDefault(),
                moduleWorkerType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));

            return new LoaderGenerationCandidate(null, diagnostics.ToImmutable());
        }

        IMethodSymbol constructor = declaredConstructors[0];
        string? configuredMethodName = GetConfiguredMethodName(target.GeneratorAttribute);
        string methodModifiers = "protected override ";

        if (configuredMethodName is not null)
        {
            IMethodSymbol? partialMethod = typeSymbol.GetMembers(configuredMethodName)
                .OfType<IMethodSymbol>()
                .FirstOrDefault(static method => method.IsPartialDefinition);

            if (partialMethod is null)
            {
                diagnostics.Add(Diagnostic.Create(
                    ModuleLoaderFactoryGeneratorDiagnostics.PartialFactoryMethodMissing,
                    typeSymbol.Locations.FirstOrDefault(),
                    configuredMethodName,
                    typeSymbol.Name));

                return new LoaderGenerationCandidate(null, diagnostics.ToImmutable());
            }

            if (!SymbolEqualityComparer.Default.Equals(partialMethod.ReturnType, moduleWorkerType))
            {
                diagnostics.Add(Diagnostic.Create(
                    ModuleLoaderFactoryGeneratorDiagnostics.PartialFactoryMethodReturnTypeMismatch,
                    partialMethod.Locations.FirstOrDefault(),
                    configuredMethodName,
                    partialMethod.ReturnType.ToDisplayString(s_fullyQualifiedFormat),
                    moduleWorkerType.ToDisplayString(s_fullyQualifiedFormat)));

                return new LoaderGenerationCandidate(null, diagnostics.ToImmutable());
            }

            if (partialMethod.Parameters is not [IParameterSymbol param1, IParameterSymbol param2]
                || !SymbolEqualityComparer.Default.Equals(param1.Type, moduleType)
                || param2.Type.ToDisplayString(s_fullyQualifiedFormat) != KnownTypes.IServiceProvider)
            {
                diagnostics.Add(Diagnostic.Create(
                    ModuleLoaderFactoryGeneratorDiagnostics.PartialFactoryMethodSignatureMismatch,
                    partialMethod.Locations.FirstOrDefault(),
                    configuredMethodName,
                    moduleType.ToDisplayString(s_fullyQualifiedFormat),
                    KnownTypes.IServiceProvider));

                return new LoaderGenerationCandidate(null, diagnostics.ToImmutable());
            }

            methodModifiers = ToAccessibilityPrefix(partialMethod.DeclaredAccessibility) + " partial ";
        }

        string namespaceName = typeSymbol.ContainingNamespace?.IsGlobalNamespace is false
            ? typeSymbol.ContainingNamespace.ToDisplayString()
            : string.Empty;

        return new LoaderGenerationCandidate(
            new LoaderGenerationModel(
                Namespace: namespaceName,
                ContractInfo: contractInfo,
                ClassSymbol: typeSymbol,
                WorkerConstructor: constructor,
                ModuleWorkerType: moduleWorkerType,
                ModuleType: moduleType,
                MethodName: configuredMethodName,
                MethodModifiers: methodModifiers),
            diagnostics.ToImmutable());
    }

    private static string? GetConfiguredMethodName(AttributeData generatorAttribute)
    {
        foreach (KeyValuePair<string, TypedConstant> namedArgument in generatorAttribute.NamedArguments)
        {
            if (namedArgument.Key == "Name" && namedArgument.Value.Value is string methodName && !string.IsNullOrWhiteSpace(methodName))
            {
                return methodName;
            }
        }

        return null;
    }

    private static INamedTypeSymbol? FindModuleLoaderBase(INamedTypeSymbol typeSymbol, INamedTypeSymbol registeredModuleLoader)
    {
        INamedTypeSymbol registeredUnbound = registeredModuleLoader.IsUnboundGenericType
            ? registeredModuleLoader
            : registeredModuleLoader.ConstructUnboundGenericType();

        for (INamedTypeSymbol? current = typeSymbol; current is not null; current = current.BaseType)
        {
            if (!current.IsGenericType)
            {
                continue;
            }

            INamedTypeSymbol currentUnbound = current.ConstructUnboundGenericType();
            if (SymbolEqualityComparer.Default.Equals(currentUnbound, registeredUnbound))
            {
                return current;
            }
        }

        return null;
    }

    private static bool ImplementsInterface(INamedTypeSymbol candidate, INamedTypeSymbol requiredInterface) =>
        SymbolEqualityComparer.Default.Equals(candidate, requiredInterface)
        || candidate.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, requiredInterface.OriginalDefinition));

    private static string ToAccessibilityPrefix(Accessibility accessibility) => accessibility switch
    {
        Accessibility.Public => "public",
        Accessibility.Protected => "protected",
        Accessibility.Internal => "internal",
        Accessibility.ProtectedOrInternal => "protected internal",
        Accessibility.ProtectedAndInternal => "private protected",
        Accessibility.Private => "private",
        _ => "private"
    };
}