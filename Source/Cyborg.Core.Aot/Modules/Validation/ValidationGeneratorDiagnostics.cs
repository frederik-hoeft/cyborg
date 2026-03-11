using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation;

internal static class ValidationGeneratorDiagnostics
{
    private const string CATEGORY = "Cyborg.Core.Aot.Modules.Validation";

    public static DiagnosticDescriptor TypeMustBePartial { get; } = new(
        id: "CYBORGVAL001",
        title: "Annotated module must be partial",
        messageFormat: "Type '{0}' must be declared partial for GeneratedModuleValidation to emit members",
        category: CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor TypeMustBeRecord { get; } = new(
        id: "CYBORGVAL002",
        title: "Annotated module must be a record",
        messageFormat: "Type '{0}' must be a record to use GeneratedModuleValidation because the generated code uses record 'with' expressions",
        category: CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor PropertyMustBeSettable { get; } = new(
        id: "CYBORGVAL003",
        title: "Annotated module property must be init/settable",
        messageFormat: "Property '{0}' on '{1}' must have an init or set accessor so the generated code can use a 'with' expression",
        category: CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor GenericTypeMismatch { get; } = new(
        id: "CYBORGVAL004",
        title: "Generic attribute type mismatch",
        messageFormat: "Property '{0}' on '{1}' has a '{2}' whose generic type does not match the property type",
        category: CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor MissingArgument { get; } = new(
        id: "CYBORGVAL005",
        title: "Missing attribute argument",
        messageFormat: "Property '{0}' on '{1}' is missing a required argument for its '{2}'",
        category: CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor UnsupportedAttributeLiteral { get; } = new(
        id: "CYBORGVAL006",
        title: "Unsupported attribute literal",
        messageFormat: "Property '{0}' on '{1}' uses an attribute value that the generator cannot re-emit as source",
        category: CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor TypeMismatch { get; } = new(
        id: "CYBORGVAL007",
        title: "Attribute type mismatch",
        messageFormat: "Property '{0}' on '{1}' has a '{2}' which is only valid on properties of type '{3}'",
        category: CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor UnsupportedValidatableTypeShape { get; } = new(
        id: "CYBORGVAL008",
        title: "Unsupported validatable type shape",
        messageFormat: "Property '{0}' uses [Validatable] type '{1}' that cannot be reconstructed by the generator (records with settable properties are required)",
        category: CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor ValidatableCycleDetected { get; } = new(
        id: "CYBORGVAL009",
        title: "Cycle detected in validatable graph",
        messageFormat: "[Validatable] traversal detected a recursive cycle on type '{0}'",
        category: CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor UnsupportedNestedPropertyShape { get; } = new(
        id: "CYBORGVAL010",
        title: "Unsupported nested property shape",
        messageFormat: "Property '{0}' on '{1}' cannot be used for nested [Validatable] generation because it is not settable or the setter is not accessible to the generator",
        category: CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor UnsupportedLengthTargetType { get; } = new(
        id: "CYBORGVAL011",
        title: "Unsupported LengthAttribute target type",
        messageFormat: "Property '{0}' on '{1}' uses LengthAttribute, but type '{2}' is neither string nor an implementation of ICollection<T>",
        category: CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor LengthArgumentMustBeNonNegative { get; } = new(
        id: "CYBORGVAL012",
        title: "LengthAttribute bound must be non-negative",
        messageFormat: "Property '{0}' on '{1}' uses LengthAttribute with invalid value '{3}' for '{2}'. Length bounds must be non-negative",
        category: CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor InvalidRangeBounds { get; } = new(
        id: "CYBORGVAL013",
        title: "Attribute bounds are invalid",
        messageFormat: "Property '{0}' on '{1}' uses {2} with Min '{3}' greater than Max '{4}'",
        category: CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor UnsupportedDefinedEnumValueTargetType { get; } = new(
        id: "CYBORGVAL014",
        title: "Unsupported DefinedEnumValueAttribute target type",
        messageFormat: "Property '{0}' on '{1}' uses DefinedEnumValueAttribute, but type '{2}' is neither an enum nor a nullable enum",
        category: CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor UnsupportedDefaultInstanceTargetType { get; } = new(
        id: "CYBORGVAL015",
        title: "Unsupported DefaultInstanceAttribute target type",
        messageFormat: "Property '{0}' on '{1}' uses DefaultInstanceAttribute, but type '{2}' must be a non-interface reference type implementing IDefaultInstance<TSelf> for itself",
        category: CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
