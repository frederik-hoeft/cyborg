using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Models;
using Microsoft.CodeAnalysis;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation;

internal abstract class PropertyAspect(bool ensuresDefault = false)
{
    public virtual bool EnsuresDefault => ensuresDefault;

    public virtual string RewriteOverrideResolutionExpression(PropertyRewriteContext context, string currentExpression, string rootPathExpression) => currentExpression;

    [return: NotNullIfNotNull(nameof(currentExpression))]
    public virtual string? RewriteDefaultAssignmentExpression(PropertyRewriteContext context, string? currentExpression) => currentExpression;

    protected virtual void EmitValidation(IndentedStringBuilder builder, ModulePropertyModel model)
    {
    }

    public void EmitValidation(
        IndentedStringBuilder builder,
        ValidationContractInfo contractInfo,
        DiagnosticsReporter diagnosticsReporter,
        PropertyModel property,
        string moduleVariableName,
        string propertyAccessExpression)
    {
        ModulePropertyModel model = new(
            Property: property,
            ContractInfo: contractInfo,
            DiagnosticsReporter: diagnosticsReporter,
            ModuleVariable: moduleVariableName,
            AccessExpression: propertyAccessExpression,
            ErrorPropertyAccessExpression: propertyAccessExpression,
            TargetType: property.Symbol.Type,
            TargetNullableTypeName: property.NullableTypeName,
            IsCollectionElement: false);
        EmitValidation(builder, model);
    }

    public void EmitCollectionElementValidation(
        IndentedStringBuilder builder,
        ValidationContractInfo contractInfo,
        DiagnosticsReporter diagnosticsReporter,
        PropertyModel property,
        string moduleVariableName,
        string propertyAccessExpression,
        string elementAccessExpression)
    {
        CollectionModel collection = property.Collection
            ?? throw new InvalidOperationException($"Property '{property.Name}' does not describe a collection.");
        ModulePropertyModel model = new(
            Property: property,
            ContractInfo: contractInfo,
            DiagnosticsReporter: diagnosticsReporter,
            ModuleVariable: moduleVariableName,
            AccessExpression: elementAccessExpression,
            ErrorPropertyAccessExpression: propertyAccessExpression,
            TargetType: collection.ElementType,
            TargetNullableTypeName: collection.ElementNullableTypeName,
            IsCollectionElement: true);
        EmitValidation(builder, model);
    }

    protected static string CreateValidationError(ModulePropertyModel model, string rule, string message) =>
        $"""
        new {model.ContractInfo.ValidationError.RenderGlobal()}({model.PropertyNameExpression}, "{rule}", $"{message}")
        """;

    protected sealed record ModulePropertyModel
    (
        PropertyModel Property,
        ValidationContractInfo ContractInfo,
        DiagnosticsReporter DiagnosticsReporter,
        string ModuleVariable,
        string AccessExpression,
        string ErrorPropertyAccessExpression,
        ITypeSymbol TargetType,
        string TargetNullableTypeName,
        bool IsCollectionElement
    )
    {
        public string PropertyNameExpression => $"nameof({ErrorPropertyAccessExpression})";

        public string TargetDescription => IsCollectionElement ? "Collection element of property" : "Property";
    }
}
