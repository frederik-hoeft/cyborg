using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Models;
using Microsoft.CodeAnalysis;
using System.Text;

namespace Cyborg.Core.Aot.Modules.Validation.Rendering;

internal sealed class ValidationSectionRenderer(ValidationContractInfo contractInfo, DiagnosticsReporter diagnosticsReporter) : ISectionRenderer
{
    public void RenderSection(IndentedStringBuilder builder, ModuleModel model)
    {
        string qualifiedType = model.FullyQualifiedTypeName;

        builder.AppendBlock(
            $$"""
            public async {{KnownTypes.ValueTaskOfT(contractInfo.ValidationResultT.RenderGlobalWithGenerics(qualifiedType))}} ValidateAsync(
                {{contractInfo.IModuleRuntime.RenderGlobal()}} runtime,
                {{KnownTypes.IServiceProvider}} serviceProvider,
                {{KnownTypes.CancellationToken}} cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                {{contractInfo.IModuleT.RenderGlobalWithGenerics(qualifiedType)}} self = this;
                {{contractInfo.IModuleT.RenderGlobalWithGenerics(qualifiedType)}} withDefaults = await self.ApplyDefaultsAsync(runtime, serviceProvider, cancellationToken);
                {{contractInfo.IModuleT.RenderGlobalWithGenerics(qualifiedType)}} withOverrides = await withDefaults.ResolveOverridesAsync(runtime, serviceProvider, cancellationToken);
                // ensure that defaults are also applied to values injected via overrides
                {{qualifiedType}} module = await withOverrides.ApplyDefaultsAsync(runtime, serviceProvider, cancellationToken);
                // interpolate all string members against the runtime environment
                module = {{ModuleValidationRenderer.ApplyInterpolation}}(module, runtime);
                {{KnownTypes.ListOfT(contractInfo.ValidationError.RenderGlobal())}} errors = [];

            """);

        builder = builder.IncreaseIndent();
        foreach (PropertyModel property in model.Properties)
        {
            AppendValidationForProperty(builder, property, "module", $"module.{property.Name}");
        }
        builder = builder.DecreaseIndent();
        builder.AppendBlock(
            $$"""
                return errors.Count == 0
                    ? {{contractInfo.ValidationResultT.RenderGlobalWithGenerics(qualifiedType)}}.Valid(module)
                    : {{contractInfo.ValidationResultT.RenderGlobalWithGenerics(qualifiedType)}}.Invalid(errors);
            }
            """);
    }

    private void AppendValidationForProperty(IndentedStringBuilder builder, PropertyModel property, string moduleVariableName, string propertyAccessExpression)
    {
        foreach (PropertyAspect aspect in property.Aspects)
        {
            aspect.EmitValidation(builder, contractInfo, diagnosticsReporter, property, moduleVariableName, propertyAccessExpression);
        }

        if (property.HasValidatableChildren)
        {
            AppendNestedValidationForProperty(builder, property, moduleVariableName, propertyAccessExpression);
        }

        if (property.HasCollectionElementChildren)
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
        string elementCurrentVariable = $"{safeIdentifier}ElementCurrent";
        bool needsCollectionEnumerationGuard = CollectionHelpers.TryConstructEnumerationGuardExpression(property, propertyAccessExpression, out string? guardCondition, out string valueExpression);
        int elementPropertyIndentLevel = 1;
        if (collection.ElementRequiresNullCheck)
        {
            elementPropertyIndentLevel++;
        }
        if (needsCollectionEnumerationGuard)
        {
            elementPropertyIndentLevel++;
        }
        StringBuilder nestedRawBuilder = new();
        IndentedStringBuilder nestedBuilder = new(nestedRawBuilder, indentLevel: builder.IndentLevel + elementPropertyIndentLevel);

        string nestedAccessExpression = elementCurrentVariable;
        foreach (PropertyModel child in collection.ElementChildren)
        {
            AppendValidationForProperty(nestedBuilder, child, moduleVariableName, $"{nestedAccessExpression}.{child.Name}");
        }

        if (nestedBuilder.Raw.Length == 0)
        {
            return;
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

        builder.AppendLine($"foreach ({collection.ElementNullableTypeName} {elementVariable} in {collectionAccessExpression})");
        builder.AppendLine("{");

        if (collection.ElementRequiresNullCheck)
        {
            (string collectionElementCheck, string collectionElementAccessExpression) = collection switch
            {
                { ElementType.IsValueType: true, IsElementNullable: true } => ($"{elementVariable}.HasValue", $"{elementVariable}.Value"),
                _ => ($"{elementVariable} is not null", elementVariable),
            };
            IndentedStringBuilder loopBuilder = builder.IncreaseIndent();
            loopBuilder.AppendBlock(
                $$"""
                if ({{collectionElementCheck}})
                {
                    {{collection.ElementNonNullableTypeName}} {{elementCurrentVariable}} = {{collectionElementAccessExpression}};
                """);
            loopBuilder.Raw.Append(nestedBuilder.Raw.ToString());
            loopBuilder.AppendLine("}");
        }
        else
        {
            IndentedStringBuilder loopBuilder = builder.IncreaseIndent();
            loopBuilder.AppendLine($"{collection.ElementNonNullableTypeName} {elementCurrentVariable} = {elementVariable};");
            loopBuilder.Raw.Append(nestedBuilder.Raw.ToString());
        }

        builder.AppendLine("}");

        if (needsCollectionEnumerationGuard)
        {
            builder = builder.DecreaseIndent();
            builder.AppendLine("}");
        }
    }

    private static string CreateSafeIdentifier(string value) => string.Concat(value.Select(static character => char.IsLetterOrDigit(character) ? character : '_'));
}
