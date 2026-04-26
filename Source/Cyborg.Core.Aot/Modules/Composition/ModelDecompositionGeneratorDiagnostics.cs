using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Composition;

internal static class ModelDecompositionGeneratorDiagnostics
{
    private const string CATEGORY = "Cyborg.Core.Aot.Modules.Composition";

    public static DiagnosticDescriptor TypeMustBePartial { get; } = new(
        id: "CYBORGCOMP001",
        title: "Annotated type must be partial",
        messageFormat: "Type '{0}' must be declared partial to use GeneratedDecompositionAttribute.",
        category: CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor NamingPolicyPropertyMissing { get; } = new(
        id: "CYBORGCOMP002",
        title: "Configured naming policy property was not found",
        messageFormat: "Type '{0}' does not contain static property '{1}' required for decomposition naming policy.",
        category: CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor NamingPolicyPropertyInvalid { get; } = new(
        id: "CYBORGCOMP003",
        title: "Configured naming policy property is invalid",
        messageFormat: "Property '{0}.{1}' must be static and assignable to '{2}' for decomposition naming policy.",
        category: CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
