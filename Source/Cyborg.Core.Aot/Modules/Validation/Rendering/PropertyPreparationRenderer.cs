using Cyborg.Core.Aot.Modules.Validation.Aspects;
using Cyborg.Core.Aot.Modules.Validation.Models;
using Cyborg.Core.Aot.Modules.Validation.Rendering.Collections;
using Cyborg.Core.Aot.Modules.Validation.Rendering.Models;
using Cyborg.Core.Aot.Modules.Validation.Rendering.Objects;
using Cyborg.Shared.Text;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Cyborg.Core.Aot.Modules.Validation.Rendering;

internal sealed class PropertyPreparationRenderer(SectionRenderer parent)
{
    public bool AppendPreparationForObject(IndentedStringBuilder builder, ImmutableArray<PropertyModel> properties, string targetVariable, string diagnosticsPhase)
    {
        List<(string PropertyName, string LocalName)> assignments = [];
        foreach (PropertyModel property in properties)
        {
            string propertyAccessExpression = $"{targetVariable}.{property.Name}";
            PropertyRewriteContext rewriteContext = new(property, parent, propertyAccessExpression);
            string? directExpression = CreatePreparedValueExpression(rewriteContext);
            bool hasDirectAssignment = !string.IsNullOrEmpty(directExpression);
            bool hasNestedValidatableAssignments = property.Object is { HasChildren: true } objectModel
                && objectModel.Children.Any(child => HasPreparationWork(child, rewriteContext));
            bool hasCollectionElementAssignments = property.Collection is { Shape.SupportsElementRewrite: true } collection
                && property.HasCollectionElementChildren
                && HasCollectionPreparationWork(collection, rewriteContext);

            if (!hasDirectAssignment && !hasNestedValidatableAssignments && !hasCollectionElementAssignments)
            {
                continue;
            }
            if (property.Symbol.SetMethod is not { } setter || !parent.VisibilityContext.IsVisible(setter))
            {
                parent.DiagnosticsReporter.Report(ValidationGeneratorDiagnostics.PropertyMustBeSettable,
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
                AppendNestedPreparationForProperty(builder, rewriteContext, localName, diagnosticsPhase);
            }

            if (hasCollectionElementAssignments)
            {
                AppendCollectionPreparationForProperty(builder, property, localName, diagnosticsPhase);
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

    public void AppendDirectPreparationForProperty(IndentedStringBuilder builder, PropertyRewriteContext rewriteContext)
    {
        string? preparedExpression = CreatePreparedValueExpression(rewriteContext);
        if (string.IsNullOrEmpty(preparedExpression))
        {
            return;
        }

        builder.AppendLine($"{rewriteContext.PropertyAccessExpression} = {preparedExpression};");
    }

    public void AppendCollectionPreparationForProperty(IndentedStringBuilder builder, PropertyModel property, string localName, string diagnosticsPhase)
    {
        CollectionModel collection = property.Collection!;
        ValueAccess access = collection.Shape.Renderer.Access(localName);
        if (access.RequiresGuard)
        {
            string collectionCurrentVariable = $"{localName}Current";
            builder.AppendBlock(
                $$"""
                if ({{access.GuardExpression}})
                {
                    {{property.NonNullableTypeName}} {{collectionCurrentVariable}} = {{access.ValueExpression}};
                """);
            AppendCollectionPreparationBody(builder.IncreaseIndent(), collection, collectionCurrentVariable, diagnosticsPhase);
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

        AppendCollectionPreparationBody(builder, collection, localName, diagnosticsPhase);
    }

    public bool HasPreparationWork(PropertyModel property, PropertyRewriteContext rewriteContext)
    {
        MutablePropertyRewriteContext mutableContext = new(property, rewriteContext.ContractInfo, rewriteContext.DiagnosticsReporter, rewriteContext.ModuleVariable,
            rewriteContext.ContextVariable, rewriteContext.PropertyAccessExpression);
        return HasPreparationWork(mutableContext);
    }

    public bool HasCollectionPreparationWork(CollectionModel collection, PropertyRewriteContext rewriteContext)
    {
        ObjectModel elementObject = collection.ElementObject
            ?? throw new InvalidOperationException("Collection preparation work detection requires validatable object metadata.");
        foreach (PropertyModel child in elementObject.Children)
        {
            MutablePropertyRewriteContext mutableContext = new(child, rewriteContext.ContractInfo, rewriteContext.DiagnosticsReporter, rewriteContext.ModuleVariable,
                rewriteContext.ContextVariable, rewriteContext.PropertyAccessExpression);
            if (HasPreparationWork(mutableContext))
            {
                return true;
            }
        }

        return false;
    }

    private void AppendNestedPreparationForProperty(IndentedStringBuilder builder, PropertyRewriteContext rewriteContext, string localName, string diagnosticsPhase)
    {
        ObjectModel objectModel = rewriteContext.Property.Object
            ?? throw new InvalidOperationException($"Nested preparation requires object metadata for property '{rewriteContext.Property.Name}'.");
        string nestedVariable = $"{localName}Current";

        objectModel.Renderer.AppendRewrite(
            builder,
            localName,
            nestedVariable,
            (nestedBuilder, currentVariable) => AppendPreparationForObject(nestedBuilder, objectModel.Children, currentVariable, diagnosticsPhase));
    }

    private void AppendCollectionPreparationBody(IndentedStringBuilder builder, CollectionModel collection, string collectionVariable, string diagnosticsPhase)
    {
        string safeIdentifier = CreateSafeIdentifier(collectionVariable);
        string rewrittenItemsVariable = $"{safeIdentifier}Items";
        string elementVariable = $"{safeIdentifier}Element";
        string elementCurrentVariable = $"{safeIdentifier}ElementCurrent";
        string elementValueVariable = $"{safeIdentifier}ElementValue";

        builder.AppendBlock(
            $$"""
            {{KnownTypes.ListOfT(collection.ElementNullableTypeName)}} {{rewrittenItemsVariable}} = [];
            foreach ({{collection.ElementNullableTypeName}} {{elementVariable}} in {{collectionVariable}})
            {
            """);

        IndentedStringBuilder loopBuilder = builder.IncreaseIndent();
        ObjectModel elementObject = collection.ElementObject
            ?? throw new InvalidOperationException("Collection element preparation requires validatable object metadata.");
        loopBuilder.AppendLine($"{collection.ElementNullableTypeName} {elementCurrentVariable} = {elementVariable};");
        elementObject.Renderer.AppendRewrite(
            loopBuilder,
            elementCurrentVariable,
            elementValueVariable,
            (elementBuilder, currentVariable) => AppendPreparationForObject(elementBuilder, elementObject.Children, currentVariable, diagnosticsPhase));
        loopBuilder.AppendLine($"{rewrittenItemsVariable}.Add({elementCurrentVariable});");

        builder.AppendLine("}");
        collection.Renderer.AppendMaterialization(builder, collectionVariable, rewrittenItemsVariable);
    }

    private bool HasPreparationWork(MutablePropertyRewriteContext rewriteContext)
    {
        string? expression = CreatePreparedValueExpression(rewriteContext);
        if (!string.IsNullOrEmpty(expression))
        {
            return true;
        }

        PropertyModel property = rewriteContext.Property;
        if (property.Object is { } objectModel)
        {
            foreach (PropertyModel child in objectModel.Children)
            {
                rewriteContext.SetProperty(child);
                if (HasPreparationWork(rewriteContext))
                {
                    return true;
                }
            }
        }

        if (property.Collection is { Shape.SupportsElementRewrite: true, ElementObject: { } elementObject })
        {
            foreach (PropertyModel child in elementObject.Children)
            {
                rewriteContext.SetProperty(child);
                if (HasPreparationWork(rewriteContext))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string? CreatePreparedValueExpression(PropertyRewriteContext context)
    {
        string? defaultExpression = null;
        foreach (IPropertyDefaultAspect aspect in context.Property.Aspects<IPropertyDefaultAspect>())
        {
            defaultExpression = aspect.RewriteDefaultAssignmentExpression(context, defaultExpression);
        }

        string expression = defaultExpression ?? context.PropertyAccessExpression;
        bool hasInvariantRewrite = false;
        foreach (IPropertyPreparationAspect aspect in context.Property.Aspects<IPropertyPreparationAspect>())
        {
            string rewritten = aspect.RewritePreparedValueExpression(context, expression);
            hasInvariantRewrite |= !string.Equals(rewritten, expression, StringComparison.Ordinal);
            expression = rewritten;
        }

        return defaultExpression is null && !hasInvariantRewrite ? null : expression;
    }

    private static string CreateSafeIdentifier(string value) => string.Concat(value.Select(static character => char.IsLetterOrDigit(character) ? character : '_'));
}
