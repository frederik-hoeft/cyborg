using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot;

internal static class Diagnostics
{
    public static DiagnosticDescriptor DuplicateContract { get; } = new(
        id: "CYBORG001",
        title: "Duplicate Contract Registration",
        messageFormat: "The contract value '{0}' is registered by multiple types: '{1}' and '{2}'.",
        category: "CyborgCoreAot",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor UnknownContract { get; } = new(
        id: "CYBORG002",
        title: "Unknown Contract Reference",
        messageFormat: "The contract value '{0}' is referenced by '{1}' but is not known by the generator",
        category: "CyborgCoreAot",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor MissingContract { get; } = new(
        id: "CYBORG003",
        title: "Missing Contract Registration",
        messageFormat: "Missing required contract registration for '{0}' of type '{1}'",
        category: "CyborgCoreAot",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}