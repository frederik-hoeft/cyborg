using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Models;
using Microsoft.CodeAnalysis;
using System.Text;

namespace Cyborg.Core.Aot.Modules.Validation.Rendering;

internal sealed class ValidationSectionRenderer(ValidationContractInfo contractInfo, DiagnosticsReporter diagnosticsReporter) : ISectionRenderer
{
    private const string CONTEXT_VARIABLE = "context";
    private const string MODULE_VARIABLE = "module";
    private const string ERRORS_VARIABLE = "errors";

    public void RenderSection(IndentedStringBuilder builder, ModuleModel model)
    {
        string qualifiedType = model.FullyQualifiedTypeName;
        string validationContextType = contractInfo.ModuleValidationContext.RenderGlobal();

        builder.AppendBlock(
            $$"""
            public async {{KnownTypes.ValueTaskOfT(contractInfo.ValidationResultT.RenderGlobalWithGenerics(qualifiedType))}} {{ModuleValidationRenderer.ValidateAsync}}(
                {{contractInfo.IModuleRuntime.RenderGlobal()}} runtime,
                {{KnownTypes.IServiceProvider}} serviceProvider,
                {{KnownTypes.CancellationToken}} cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                {{validationContextType}} {{CONTEXT_VARIABLE}} = {{validationContextType}}.Create(runtime, serviceProvider);
                // apply defaults first to avoid overriding properties of null objects
                {{qualifiedType}} {{MODULE_VARIABLE}} = await this.{{ModuleValidationRenderer.ApplyDefaultsAsync}}({{CONTEXT_VARIABLE}}, cancellationToken);
                // resolve any overrides that may have been applied to the module
                {{MODULE_VARIABLE}} = await {{MODULE_VARIABLE}}.{{ModuleValidationRenderer.ResolveOverridesAsync}}({{CONTEXT_VARIABLE}}, cancellationToken);
                // ensure that defaults are also applied to values injected via overrides
                {{MODULE_VARIABLE}} = await {{MODULE_VARIABLE}}.{{ModuleValidationRenderer.ApplyDefaultsAsync}}({{CONTEXT_VARIABLE}}, cancellationToken);
                // interpolate all string members against the runtime environment
                {{MODULE_VARIABLE}} = await {{MODULE_VARIABLE}}.{{ModuleValidationRenderer.ApplyInterpolationAsync}}({{CONTEXT_VARIABLE}}, cancellationToken);
                {{KnownTypes.ListOfT(contractInfo.ValidationError.RenderGlobal())}} {{ERRORS_VARIABLE}} = [];

            """);

        builder = builder.IncreaseIndent();
        foreach (PropertyModel property in model.Properties)
        {
            AppendValidationForProperty(builder, property, MODULE_VARIABLE, $"{MODULE_VARIABLE}.{property.Name}");
        }
        builder = builder.DecreaseIndent();
        builder.AppendBlock(
            $$"""
                return {{ERRORS_VARIABLE}}.Count == 0
                    ? {{contractInfo.ValidationResultT.RenderGlobalWithGenerics(qualifiedType)}}.Valid({{MODULE_VARIABLE}})
                    : {{contractInfo.ValidationResultT.RenderGlobalWithGenerics(qualifiedType)}}.Invalid({{ERRORS_VARIABLE}});
            }
            """);
    }

    private void AppendValidationForProperty(IndentedStringBuilder builder, PropertyModel property, string moduleVariableName, string propertyAccessExpression)
    {
        foreach (PropertyAspect aspect in property.Aspects)
        {
            if (aspect is not CollectionElementValidationAspect)
            {
                aspect.EmitValidation(builder, contractInfo, diagnosticsReporter, property, moduleVariableName, propertyAccessExpression);
            }
        }

        if (property.HasValidatableChildren)
        {
            AppendNestedValidationForProperty(builder, property, moduleVariableName, propertyAccessExpression);
        }

        if (property.HasCollectionValidationWork)
        {
            AppendCollectionValidationForProperty(builder, property, moduleVariableName, propertyAccessExpression);
        }
    }

    private void AppendNestedValidationForProperty(IndentedStringBuilder builder, PropertyModel property, string moduleVariableName, string propertyAccessExpression)
    {
        StringBuilder nestedRawBuilder = new();
        IndentedStringBuilder nestedBuilder = new(nestedRawBuilder, indentLevel: builder.IndentLevel + 1);

        foreach (PropertyModel child in property.Children)
        {
            AppendValidationForProperty(nestedBuilder, child, moduleVariableName, $"{propertyAccessExpression}.{child.Name}");
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

    private void AppendCollectionValidationForProperty(IndentedStringBuilder builder, PropertyModel property, string moduleVariableName, string propertyAccessExpression)
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
                    contractInfo,
                    diagnosticsReporter,
                    property,
                    moduleVariableName,
                    propertyAccessExpression,
                    elementVariable,
                    indexVariable);
            }
        }

        if (property.HasCollectionElementChildren)
        {
            AppendCollectionElementChildValidation(loopBuilder, property, moduleVariableName, elementVariable);
        }
        loopBuilder.AppendLine($"++{indexVariable};");
        builder.AppendLine("}");

        if (needsCollectionEnumerationGuard)
        {
            builder = builder.DecreaseIndent();
            builder.AppendLine("}");
        }
    }

    private void AppendCollectionElementChildValidation(IndentedStringBuilder builder, PropertyModel property, string moduleVariableName, string elementVariable)
    {
        CollectionModel collection = property.Collection!;
        string elementCurrentVariable = $"{elementVariable}Current";
        int validationIndentLevel = builder.IndentLevel + (collection.ElementRequiresNullCheck ? 1 : 0);
        StringBuilder validationRawBuilder = new();
        IndentedStringBuilder validationBuilder = new(validationRawBuilder, validationIndentLevel);
        foreach (PropertyModel child in collection.ElementChildren)
        {
            AppendValidationForProperty(validationBuilder, child, moduleVariableName, $"{elementCurrentVariable}.{child.Name}");
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
}
