using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Loaders;

internal static class ModuleLoaderFactoryGeneratorDiagnostics
{
    private const string CATEGORY = "Cyborg.Core.Aot.Modules.Loaders";

    public static DiagnosticDescriptor PartialFactoryMethodMissing { get; } = new(
        id: "CYBORGMLF001",
        title: "Partial factory method not found",
        messageFormat: "The partial factory method '{0}' was not found on '{1}'.",
        category: CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor PartialFactoryMethodReturnTypeMismatch { get; } = new(
        id: "CYBORGMLF002",
        title: "Partial factory method return type mismatch",
        messageFormat: "Partial factory method '{0}' has return type '{1}', but the generated worker type is '{2}'.",
        category: CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor PartialFactoryMethodSignatureMismatch { get; } = new(
        id: "CYBORGMLF003",
        title: "Partial factory method signature mismatch",
        messageFormat: "Partial factory method '{0}' must have parameters '({1} module, {2} serviceProvider)'.",
        category: CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor TargetMustBeClass { get; } = new(
        id: "CYBORGMLF004",
        title: "Generator target must be a class",
        messageFormat: "Type '{0}' must be a class to use GeneratedModuleLoaderFactoryAttribute.",
        category: CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor TargetMustInheritModuleLoader { get; } = new(
        id: "CYBORGMLF005",
        title: "Generator target must inherit from ModuleLoader",
        messageFormat: "Type '{0}' must inherit from '{1}' to use GeneratedModuleLoaderFactoryAttribute.",
        category: CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor UnexpectedModuleLoaderShape { get; } = new(
        id: "CYBORGMLF006",
        title: "Unexpected ModuleLoader shape",
        messageFormat: "Type '{0}' inherits from '{1}', but the generator could not extract the expected generic arguments.",
        category: CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor ModuleWorkerTypeMustBeNamedType { get; } = new(
        id: "CYBORGMLF007",
        title: "Module worker type must be a named type",
        messageFormat: "The module worker type '{0}' on '{1}' must be a named type.",
        category: CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor ModuleWorkerMustImplementInterface { get; } = new(
        id: "CYBORGMLF008",
        title: "Module worker must implement the required interface",
        messageFormat: "The module worker type '{0}' must implement '{1}'.",
        category: CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor ModuleWorkerMustHaveSingleConstructor { get; } = new(
        id: "CYBORGMLF009",
        title: "Module worker must have a single constructor",
        messageFormat: "The module worker type '{0}' must declare exactly one constructor for code generation.",
        category: CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
