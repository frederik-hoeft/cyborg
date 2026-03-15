using System.Collections.Immutable;
using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Models;

namespace Cyborg.Core.Aot.Modules.Validation.Rendering;

internal sealed class DefaultsSectionRenderer(ValidationContractInfo contractInfo, string rootModuleVariable, DiagnosticsReporter diagnosticsReporter) : ISectionRenderer
{
    public void RenderSection(IndentedStringBuilder builder, ModuleModel model)
    {
        string qualifiedType = model.FullyQualifiedTypeName;
        builder.AppendBlock(
            $$"""
            async {{KnownTypes.ValueTaskOfT(qualifiedType)}} {{contractInfo.IModuleT.RenderGlobalWithGenerics(qualifiedType)}}.ApplyDefaultsAsync(
                {{contractInfo.IModuleRuntime.RenderGlobal()}} runtime,
                {{KnownTypes.IServiceProvider}} serviceProvider,
                {{KnownTypes.CancellationToken}} cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                {{qualifiedType}} {{rootModuleVariable}} = this;

            """);

        builder = builder.IncreaseIndent();
        AppendDefaultApplicationForObject(builder, model.Properties, rootModuleVariable);
        builder = builder.DecreaseIndent();
        builder.AppendBlock(
            """
                await global::System.Threading.Tasks.Task.CompletedTask;
                return self;
            }
            """);
    }

    private bool AppendDefaultApplicationForObject(IndentedStringBuilder builder, ImmutableArray<PropertyModel> properties, string targetVariable)
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

            string localName = $"{targetVariable}_{property.Name}";
            string localInitializer = directExpression ?? propertyAccessExpression;
            builder.AppendLine($"{property.NullableTypeName} {localName} = {localInitializer};");

            if (hasNestedValidatableAssignments)
            {
                AppendNestedDefaultApplicationForProperty(builder, rewriteContext, localName);
            }

            if (hasCollectionElementAssignments)
            {
                AppendCollectionDefaultApplicationForProperty(builder, property, localName);
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

    private void AppendNestedDefaultApplicationForProperty(IndentedStringBuilder builder, PropertyRewriteContext rewriteContext, string localName)
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
            AppendDefaultApplicationForObject(builder.IncreaseIndent(), property.Children, nestedVariable);
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
        AppendDefaultApplicationForObject(builder, property.Children, nestedVariable);
        builder.AppendLine($"{localName} = {nestedVariable};");
    }

    private void AppendCollectionDefaultApplicationForProperty(IndentedStringBuilder builder, PropertyModel property, string localName)
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
            AppendCollectionDefaultApplicationBody(builder.IncreaseIndent(), collection, collectionCurrentVariable);
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

        AppendCollectionDefaultApplicationBody(builder, collection, localName);
    }

    private void AppendCollectionDefaultApplicationBody(IndentedStringBuilder builder, CollectionModel collection, string collectionVariable)
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
            AppendDefaultApplicationForObject(loopBuilder.IncreaseIndent(), collection.ElementChildren, elementValueVariable);
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
            AppendDefaultApplicationForObject(loopBuilder, collection.ElementChildren, elementCurrentVariable);
            loopBuilder.AppendLine($"{rewrittenItemsVariable}.Add({elementCurrentVariable});");
        }

        builder.AppendLine("}");
        AppendCollectionMaterialization(builder, collection, collectionVariable, rewrittenItemsVariable);
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

    private static bool HasDefaultWork(PropertyModel property, PropertyRewriteContext rewriteContext)
    {
        MutablePropertyRewriteContext mutableContext = new(property, rewriteContext.ContractInfo, rewriteContext.DiagnosticsReporter, rewriteContext.ModuleVariable, rewriteContext.PropertyAccessExpression);
        return HasDefaultWork(mutableContext);
    }

    private static bool HasDefaultWork(MutablePropertyRewriteContext rewriteContext)
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

    private static bool HasCollectionDefaultWork(CollectionModel collection, PropertyRewriteContext rewriteContext)
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
