using System.Text;
using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Models;

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
                {{qualifiedType}} module = await withDefaults.ResolveOverridesAsync(runtime, serviceProvider, cancellationToken);
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
        foreach (PropertyValidationAspect aspect in property.Aspects)
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
        bool collectionPropertyRequiresNullCheck = property.IsNullable || !property.Symbol.Type.IsValueType;
        int elementPropertyIndentLevel = 1;
        if (collection.ElementRequiresNullCheck)
        {
            elementPropertyIndentLevel++;
        }
        if (collectionPropertyRequiresNullCheck)
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

        if (collectionPropertyRequiresNullCheck)
        {
            string collectionCurrentVariable = $"{safeIdentifier}CollectionCurrent";
            builder.AppendBlock(
                $$"""
                if ({{propertyAccessExpression}} is not null)
                {
                    {{property.NonNullableTypeName}} {{collectionCurrentVariable}} = {{propertyAccessExpression}};
                """);
            builder = builder.IncreaseIndent();
            collectionAccessExpression = collectionCurrentVariable;
        }

        builder.AppendLine($"foreach ({collection.ElementNullableTypeName} {elementVariable} in {collectionAccessExpression})");
        builder.AppendLine("{");

        if (collection.ElementRequiresNullCheck)
        {
            IndentedStringBuilder loopBuilder = builder.IncreaseIndent();
            loopBuilder.AppendBlock(
                $$"""
                if ({{elementVariable}} is not null)
                {
                    {{collection.ElementNonNullableTypeName}} {{elementCurrentVariable}} = {{elementVariable}};
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

        if (collectionPropertyRequiresNullCheck)
        {
            builder = builder.DecreaseIndent();
            builder.AppendLine("}");
        }
    }

    private static string CreateSafeIdentifier(string value) => string.Concat(value.Select(static character => char.IsLetterOrDigit(character) ? character : '_'));
}
