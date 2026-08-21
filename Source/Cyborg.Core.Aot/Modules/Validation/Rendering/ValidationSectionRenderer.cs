using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Models;
using Microsoft.CodeAnalysis;
using System.Text;

namespace Cyborg.Core.Aot.Modules.Validation.Rendering;

internal sealed class ValidationSectionRenderer(ValidationContractInfo contractInfo, VisibilityContext visibilityContext, DiagnosticsReporter diagnosticsReporter)
    : SectionRenderer(contractInfo, visibilityContext, diagnosticsReporter)
{
    private ValidationVariables Variables => field ??= new ValidationVariables(RootModuleVariable, ContextVariable, Errors: "errors");

    public override void RenderSection(IndentedStringBuilder builder, ModuleModel model)
    {
        string qualifiedType = model.FullyQualifiedTypeName;
        string validationContextType = ContractInfo.ModuleValidationContext.RenderGlobal();

        builder.AppendBlock(
            $$"""
            public async {{KnownTypes.ValueTaskOfT(ContractInfo.IValidationResultT.RenderGlobalWithGenerics(qualifiedType))}} {{ModuleValidationRenderer.ValidateAsync}}(
                {{ContractInfo.IModuleRuntime.RenderGlobal()}} runtime,
                {{KnownTypes.IServiceProvider}} serviceProvider,
                {{KnownTypes.CancellationToken}} cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                {{validationContextType}} {{Variables.Context}} = {{validationContextType}}.Create(runtime, serviceProvider);
                // apply defaults first to avoid overriding properties of null objects
                {{qualifiedType}} {{Variables.Module}} = await this.{{ModuleValidationRenderer.ApplyDefaultsAsync}}({{Variables.Context}}, cancellationToken);
                // resolve any overrides that may have been applied to the module
                {{Variables.Module}} = await {{Variables.Module}}.{{ModuleValidationRenderer.ResolveOverridesAsync}}({{Variables.Context}}, cancellationToken);
                // ensure that defaults are also applied to values injected via overrides
                {{Variables.Module}} = await {{Variables.Module}}.{{ModuleValidationRenderer.ApplyDefaultsAsync}}({{Variables.Context}}, cancellationToken);
                // interpolate all string members against the runtime environment
                {{Variables.Module}} = await {{Variables.Module}}.{{ModuleValidationRenderer.ApplyInterpolationAsync}}({{Variables.Context}}, cancellationToken);
                {{KnownTypes.ListOfT(ContractInfo.ValidationError.RenderGlobal())}} {{Variables.Errors}} = [];

            """);

        builder = builder.IncreaseIndent();
        foreach (PropertyModel property in model.Properties)
        {
            AppendValidationForProperty(builder, property, $"{Variables.Module}.{property.Name}");
        }
        builder = builder.DecreaseIndent();
        builder.AppendBlock(
            $$"""
                return {{Variables.Errors}}.Count == 0
                    ? {{ContractInfo.ValidationResult.RenderGlobal()}}.Valid({{Variables.Module}})
                    : {{ContractInfo.ValidationResult.RenderGlobal()}}.Invalid({{Variables.Module}}, {{Variables.Errors}});
            }
            """);
    }

    private void AppendValidationForProperty(IndentedStringBuilder builder, PropertyModel property, string propertyAccessExpression)
    {
        foreach (PropertyAspect aspect in property.Aspects)
        {
            if (aspect is not CollectionElementValidationAspect)
            {
                aspect.EmitValidation(builder, ContractInfo, DiagnosticsReporter, property, Variables, propertyAccessExpression);
            }
        }

        if (property.HasValidatableChildren)
        {
            AppendNestedValidationForProperty(builder, property, propertyAccessExpression);
        }

        if (property.HasCollectionValidationWork)
        {
            AppendCollectionValidationForProperty(builder, property, propertyAccessExpression);
        }
    }

    private void AppendNestedValidationForProperty(IndentedStringBuilder builder, PropertyModel property, string propertyAccessExpression)
    {
        StringBuilder nestedRawBuilder = new();
        IndentedStringBuilder nestedBuilder = new(nestedRawBuilder, indentLevel: builder.IndentLevel + 1);

        foreach (PropertyModel child in property.Children)
        {
            AppendValidationForProperty(nestedBuilder, child, $"{propertyAccessExpression}.{child.Name}");
        }

        if (nestedRawBuilder.Length == 0)
        {
            return;
        }

        builder.AppendBlock(
            $$"""
            if ({{propertyAccessExpression}} is not null)
            {
            """);
        builder.Raw.Append(nestedRawBuilder.ToString());
        builder.AppendLine("}");
    }

    private void AppendCollectionValidationForProperty(IndentedStringBuilder builder, PropertyModel property, string propertyAccessExpression)
    {
        CollectionModel collection = property.Collection!;
        string safeIdentifier = CreateSafeIdentifier(propertyAccessExpression);
        string collectionAccessExpression = propertyAccessExpression;
        string elementVariable = $"{safeIdentifier}Element";
        bool needsCollectionEnumerationGuard = CollectionHelpers.TryConstructEnumerationGuardExpression(property, propertyAccessExpression, out string? guardCondition, out string valueExpression);
        if (!needsCollectionEnumerationGuard && property.Symbol.Type.IsReferenceType)
        {
            needsCollectionEnumerationGuard = true;
            guardCondition = $"{propertyAccessExpression} is not null";
        }

        if (needsCollectionEnumerationGuard)
        {
            string collectionCurrentVariable = $"{safeIdentifier}CollectionCurrent";
            builder.AppendBlock(
                $$"""
                if ({{guardCondition}})
                {
                    {{property.NonNullableTypeName}} {{collectionCurrentVariable}} = {{valueExpression}};
                """);
            builder = builder.IncreaseIndent();
            collectionAccessExpression = collectionCurrentVariable;
        }

        string indexVariable = $"{safeIdentifier}Index";
        builder.AppendBlock(
            $$"""
            int {{indexVariable}} = 0;
            foreach ({{collection.ElementNullableTypeName}} {{elementVariable}} in {{collectionAccessExpression}})
            {
            """);
        IndentedStringBuilder loopBuilder = builder.IncreaseIndent();

        if (property.TryGetAspects(out List<CollectionElementValidationAspect>? elementValidationAspects))
        {
            foreach (CollectionElementValidationAspect elementValidationAspect in elementValidationAspects)
            {
                elementValidationAspect.ValidationAspect.EmitCollectionElementValidation(
                    loopBuilder,
                    ContractInfo,
                    DiagnosticsReporter,
                    property,
                    Variables,
                    propertyAccessExpression,
                    elementVariable,
                    indexVariable);
            }
        }

        if (property.HasCollectionElementChildren)
        {
            AppendCollectionElementChildValidation(loopBuilder, property, elementVariable);
        }
        loopBuilder.AppendLine($"++{indexVariable};");
        builder.AppendLine("}");

        if (needsCollectionEnumerationGuard)
        {
            builder = builder.DecreaseIndent();
            builder.AppendLine("}");
        }
    }

    private void AppendCollectionElementChildValidation(IndentedStringBuilder builder, PropertyModel property, string elementVariable)
    {
        CollectionModel collection = property.Collection!;
        string elementCurrentVariable = $"{elementVariable}Current";
        int validationIndentLevel = builder.IndentLevel + (collection.ElementRequiresNullCheck ? 1 : 0);
        StringBuilder validationRawBuilder = new();
        IndentedStringBuilder validationBuilder = new(validationRawBuilder, validationIndentLevel);
        foreach (PropertyModel child in collection.ElementChildren)
        {
            AppendValidationForProperty(validationBuilder, child, $"{elementCurrentVariable}.{child.Name}");
        }
        if (validationRawBuilder.Length == 0)
        {
            return;
        }

        if (collection.ElementRequiresNullCheck)
        {
            (string collectionElementCheck, string collectionElementAccessExpression) = collection switch
            {
                { ElementType.IsValueType: true, IsElementNullable: true } => ($"{elementVariable}.HasValue", $"{elementVariable}.Value"),
                _ => ($"{elementVariable} is not null", elementVariable),
            };
            builder.AppendBlock(
                $$"""
                if ({{collectionElementCheck}})
                {
                    {{collection.ElementNonNullableTypeName}} {{elementCurrentVariable}} = {{collectionElementAccessExpression}};
                """);
            builder.Raw.Append(validationRawBuilder.ToString());
            builder.AppendLine("}");
            return;
        }

        builder.AppendLine($"{collection.ElementNonNullableTypeName} {elementCurrentVariable} = {elementVariable};");
        builder.Raw.Append(validationRawBuilder.ToString());
    }

    private static string CreateSafeIdentifier(string value) => string.Concat(value.Select(static character => char.IsLetterOrDigit(character) ? character : '_'));

    public sealed record ValidationVariables(string Module, string Context, string Errors);
}
