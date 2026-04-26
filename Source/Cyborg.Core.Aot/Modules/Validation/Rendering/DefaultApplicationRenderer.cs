using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Models;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Cyborg.Core.Aot.Modules.Validation.Rendering;

internal sealed class DefaultApplicationRenderer(ValidationContractInfo contractInfo, string rootModuleVariable, DiagnosticsReporter diagnosticsReporter)
{
    public bool AppendDefaultApplicationForObject(IndentedStringBuilder builder, ImmutableArray<PropertyModel> properties, string targetVariable, string diagnosticsPhase)
    {
        List<(string PropertyName, string LocalName)> assignments = [];
        foreach (PropertyModel property in properties)
        {
            string propertyAccessExpression = $"{targetVariable}.{property.Name}";
            PropertyRewriteContext rewriteContext = new(property, contractInfo, diagnosticsReporter, rootModuleVariable, propertyAccessExpression);
            string? directExpression = CreateDefaultAssignmentExpression(rewriteContext);
            bool hasDirectAssignment = !string.IsNullOrEmpty(directExpression);
            bool hasNestedValidatableAssignments = property.HasValidatableChildren && property.Children.Any(child => HasDefaultWork(child, rewriteContext));
            bool hasCollectionElementAssignments = property.Collection is { SupportsElementRewrite: true } collection
                && property.HasCollectionElementChildren
                && HasCollectionDefaultWork(collection, rewriteContext);

            if (!hasDirectAssignment && !hasNestedValidatableAssignments && !hasCollectionElementAssignments)
            {
                continue;
            }
            if (property.Symbol.SetMethod is not { } setter || !contractInfo.Compilation.IsSymbolAccessibleWithin(setter, property.Symbol.Type))
            {
                diagnosticsReporter.Report(ValidationGeneratorDiagnostics.PropertyMustBeSettable,
                    property.Symbol.Locations.FirstOrDefault() ?? Location.None,
                    property.Symbol.Name,
                    property.Symbol.ContainingType,
                    diagnosticsPhase);
                continue;
            }

            string localName = $"{targetVariable}_{property.Name}";
            string localInitializer = directExpression ?? propertyAccessExpression;
            builder.AppendLine($"{property.NullableTypeName} {localName} = {localInitializer};");

            if (hasNestedValidatableAssignments)
            {
                AppendNestedDefaultApplicationForProperty(builder, rewriteContext, localName, diagnosticsPhase);
            }

            if (hasCollectionElementAssignments)
            {
                AppendCollectionDefaultApplicationForProperty(builder, property, localName, diagnosticsPhase);
            }

            assignments.Add((property.Name, localName));
        }

        if (assignments.Count == 0)
        {
            return false;
        }

        builder.AppendLine($"{targetVariable} = {targetVariable} with {{ {string.Join(", ", assignments.Select(static assignment => $"{assignment.PropertyName} = {assignment.LocalName}"))} }};");
        return true;
    }

    public void AppendDirectDefaultApplicationForProperty(IndentedStringBuilder builder, PropertyModel property, string propertyAccessExpression)
    {
        PropertyRewriteContext rewriteContext = new(property, contractInfo, diagnosticsReporter, rootModuleVariable, propertyAccessExpression);
        string? defaultExpression = CreateDefaultAssignmentExpression(rewriteContext);
        if (string.IsNullOrEmpty(defaultExpression))
        {
            return;
        }

        builder.AppendLine($"{propertyAccessExpression} = {defaultExpression};");
    }

    public void AppendCollectionDefaultApplicationForProperty(IndentedStringBuilder builder, PropertyModel property, string localName, string diagnosticsPhase)
    {
        CollectionModel collection = property.Collection!;
        if (property.IsNullable || (!property.HasDefault && !property.Symbol.Type.IsValueType))
        {
            string collectionCurrentVariable = $"{localName}Current";
            builder.AppendBlock(
                $$"""
                if ({{localName}} is not null)
                {
                    {{property.NonNullableTypeName}} {{collectionCurrentVariable}} = {{localName}};
                """);
            AppendCollectionDefaultApplicationBody(builder.IncreaseIndent(), collection, collectionCurrentVariable, diagnosticsPhase);
            builder.AppendBlock(
                $$"""
                    {{localName}} = {{collectionCurrentVariable}};
                }
                """);
            if (!property.IsNullable)
            {
                builder.AppendLine($"{ModuleValidationRenderer.Helpers}.{ModuleValidationRenderer.HelperMembers.NullableRelax}({localName});");
            }
            return;
        }

        AppendCollectionDefaultApplicationBody(builder, collection, localName, diagnosticsPhase);
    }

    public bool HasDefaultWork(PropertyModel property, PropertyRewriteContext rewriteContext)
    {
        MutablePropertyRewriteContext mutableContext = new(property, rewriteContext.ContractInfo, rewriteContext.DiagnosticsReporter, rewriteContext.ModuleVariable, rewriteContext.PropertyAccessExpression);
        return HasDefaultWork(mutableContext);
    }

    public bool HasCollectionDefaultWork(CollectionModel collection, PropertyRewriteContext rewriteContext)
    {
        foreach (PropertyModel child in collection.ElementChildren)
        {
            MutablePropertyRewriteContext mutableContext = new(child, rewriteContext.ContractInfo, rewriteContext.DiagnosticsReporter, rewriteContext.ModuleVariable, rewriteContext.PropertyAccessExpression);
            if (HasDefaultWork(mutableContext))
            {
                return true;
            }
        }

        return false;
    }

    private void AppendNestedDefaultApplicationForProperty(IndentedStringBuilder builder, PropertyRewriteContext rewriteContext, string localName, string diagnosticsPhase)
    {
        string nestedVariable = $"{localName}Current";

        PropertyModel property = rewriteContext.Property;
        if (property.IsNullable || !property.HasDefault)
        {
            builder.AppendBlock(
                $$"""
                if ({{localName}} is not null)
                {
                    {{property.NonNullableTypeName}} {{nestedVariable}} = {{localName}};
                """);
            AppendDefaultApplicationForObject(builder.IncreaseIndent(), property.Children, nestedVariable, diagnosticsPhase);
            builder.AppendBlock(
                $$"""
                    {{localName}} = {{nestedVariable}};
                }
                """);
            if (!property.IsNullable)
            {
                builder.AppendLine($"{ModuleValidationRenderer.Helpers}.{ModuleValidationRenderer.HelperMembers.NullableRelax}({localName});");
            }
            return;
        }

        builder.AppendLine($"{property.NonNullableTypeName} {nestedVariable} = {localName};");
        AppendDefaultApplicationForObject(builder, property.Children, nestedVariable, diagnosticsPhase);
        builder.AppendLine($"{localName} = {nestedVariable};");
    }

    private void AppendCollectionDefaultApplicationBody(IndentedStringBuilder builder, CollectionModel collection, string collectionVariable, string diagnosticsPhase)
    {
        string safeIdentifier = CreateSafeIdentifier(collectionVariable);
        string rewrittenItemsVariable = $"{safeIdentifier}Items";
        string elementVariable = $"{safeIdentifier}Element";
        string elementCurrentVariable = $"{safeIdentifier}ElementCurrent";
        string elementValueVariable = $"{safeIdentifier}ElementValue";

        builder.AppendLine($"{KnownTypes.ListOfT(collection.ElementNullableTypeName)} {rewrittenItemsVariable} = [];");
        builder.AppendLine($"foreach ({collection.ElementNullableTypeName} {elementVariable} in {collectionVariable})");
        builder.AppendLine("{");

        IndentedStringBuilder loopBuilder = builder.IncreaseIndent();
        if (collection.ElementRequiresNullCheck)
        {
            loopBuilder.AppendLine($"{collection.ElementNullableTypeName} {elementCurrentVariable} = {elementVariable};");
            loopBuilder.AppendBlock(
                $$"""
                if ({{elementCurrentVariable}} is not null)
                {
                    {{collection.ElementNonNullableTypeName}} {{elementValueVariable}} = {{elementCurrentVariable}};
                """);
            AppendDefaultApplicationForObject(loopBuilder.IncreaseIndent(), collection.ElementChildren, elementValueVariable, diagnosticsPhase);
            loopBuilder.AppendBlock(
                $$"""
                    {{elementCurrentVariable}} = {{elementValueVariable}};
                }
                """);
            loopBuilder.AppendLine($"{ModuleValidationRenderer.Helpers}.{ModuleValidationRenderer.HelperMembers.NullableRelax}({elementCurrentVariable});");
            loopBuilder.AppendLine($"{rewrittenItemsVariable}.Add({elementCurrentVariable});");
        }
        else
        {
            loopBuilder.AppendLine($"{collection.ElementNonNullableTypeName} {elementCurrentVariable} = {elementVariable};");
            AppendDefaultApplicationForObject(loopBuilder, collection.ElementChildren, elementCurrentVariable, diagnosticsPhase);
            loopBuilder.AppendLine($"{rewrittenItemsVariable}.Add({elementCurrentVariable});");
        }

        builder.AppendLine("}");
        AppendCollectionMaterialization(builder, collection, collectionVariable, rewrittenItemsVariable);
    }

    private bool HasDefaultWork(MutablePropertyRewriteContext rewriteContext)
    {
        string? expression = CreateDefaultAssignmentExpression(rewriteContext);
        if (!string.IsNullOrEmpty(expression))
        {
            return true;
        }

        PropertyModel property = rewriteContext.Property;
        if (property.IsValidatableType)
        {
            foreach (PropertyModel child in property.Children)
            {
                rewriteContext.SetProperty(child);
                if (HasDefaultWork(rewriteContext))
                {
                    return true;
                }
            }
        }

        if (property.Collection is { SupportsElementRewrite: true, IsElementValidatableType: true } collection)
        {
            foreach (PropertyModel child in collection.ElementChildren)
            {
                rewriteContext.SetProperty(child);
                if (HasDefaultWork(rewriteContext))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void AppendCollectionMaterialization(IndentedStringBuilder builder, CollectionModel collection, string targetVariable, string rewrittenItemsVariable)
    {
        switch (collection.MaterializationKind)
        {
            case CollectionMaterializationKind.UseList:
                builder.AppendLine($"{targetVariable} = {rewrittenItemsVariable};");
                break;
            case CollectionMaterializationKind.UseArray:
                builder.AppendLine($"{targetVariable} = {KnownTypes.Enumerable}.ToArray({rewrittenItemsVariable});");
                break;
            case CollectionMaterializationKind.UseImmutableArray:
                builder.AppendLine($"{targetVariable} = {KnownTypes.ImmutableArray}.CreateRange({rewrittenItemsVariable});");
                break;
            case CollectionMaterializationKind.ConstructFromList:
                builder.AppendLine($"{targetVariable} = new {collection.MaterializationTypeName}({rewrittenItemsVariable});");
                break;
            case CollectionMaterializationKind.ParameterlessAdd:
                string safeIdentifier = CreateSafeIdentifier(targetVariable);
                string rewrittenCollectionVariable = $"{safeIdentifier}Collection";
                string rewrittenCollectionItemsVariable = $"{safeIdentifier}CollectionItems";
                string rewrittenItemVariable = $"{safeIdentifier}Item";
                builder.AppendLine($"{collection.MaterializationTypeName} {rewrittenCollectionVariable} = new();");
                builder.AppendLine($"{KnownTypes.ICollectionOfT(collection.ElementNullableTypeName)} {rewrittenCollectionItemsVariable} = {rewrittenCollectionVariable};");
                builder.AppendLine($"foreach ({collection.ElementNullableTypeName} {rewrittenItemVariable} in {rewrittenItemsVariable})");
                builder.AppendLine("{");
                builder.IncreaseIndent().AppendLine($"{rewrittenCollectionItemsVariable}.Add({rewrittenItemVariable});");
                builder.AppendLine("}");
                builder.AppendLine($"{targetVariable} = {rewrittenCollectionVariable};");
                break;
            default:
                throw new InvalidOperationException("Unsupported collection materialization kind.");
        }
    }

    private static string? CreateDefaultAssignmentExpression(PropertyRewriteContext context)
    {
        string? expression = null;
        foreach (PropertyValidationAspect aspect in context.Property.Aspects)
        {
            expression = aspect.RewriteDefaultAssignmentExpression(context, expression);
        }
        return expression;
    }

    private static string CreateSafeIdentifier(string value) => string.Concat(value.Select(static character => char.IsLetterOrDigit(character) ? character : '_'));
}
