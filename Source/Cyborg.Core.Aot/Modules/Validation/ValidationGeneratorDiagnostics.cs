using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation;

internal static class ValidationGeneratorDiagnostics
{
    public static DiagnosticDescriptor TypeMustBePartial { get; } = new(
        id: "CYBORGVAL001",
        title: "Annotated module must be partial",
        messageFormat: "Type '{0}' must be declared partial for GeneratedModuleValidation to emit members",
        category: "Cyborg.Aot.Validation",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor TypeMustBeRecord { get; } = new(
        id: "CYBORGVAL002",
        title: "Annotated module must be a record",
        messageFormat: "Type '{0}' must be a record to use GeneratedModuleValidation because the generated code uses record 'with' expressions",
        category: "Cyborg.Aot.Validation",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor PropertyMustBeSettable { get; } = new(
        id: "CYBORGVAL003",
        title: "Annotated module property must be init/settable",
        messageFormat: "Property '{0}' on '{1}' must have an init or set accessor so the generated code can use a 'with' expression",
        category: "Cyborg.Aot.Validation",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor GenericTypeMismatch { get; } = new(
        id: "CYBORGVAL004",
        title: "Generic attribute type mismatch",
        messageFormat: "Property '{0}' on '{1}' has a '{2}' whose generic type does not match the property type",
        category: "Cyborg.Aot.Validation",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor MissingArgument { get; } = new(
        id: "CYBORGVAL005",
        title: "Missing attribute argument",
        messageFormat: "Property '{0}' on '{1}' is missing a required argument for its '{2}'",
        category: "Cyborg.Aot.Validation",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor UnsupportedAttributeLiteral { get; } = new(
        id: "CYBORGVAL006",
        title: "Unsupported attribute literal",
        messageFormat: "Property '{0}' on '{1}' uses an attribute value that the generator cannot re-emit as source",
        category: "Cyborg.Aot.Validation",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor TypeMismatch { get; } = new(
        id: "CYBORGVAL007",
        title: "Attribute type mismatch",
        messageFormat: "Property '{0}' on '{1}' has a '{2}' which is only valid on properties of type '{3}'",
        category: "Cyborg.Aot.Validation",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
