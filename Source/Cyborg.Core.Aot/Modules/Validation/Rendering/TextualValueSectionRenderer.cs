using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Models;
using Cyborg.Core.Aot.Modules.Validation.Rendering.Collections;
using Cyborg.Core.Aot.Modules.Validation.Rendering.Models;
using Cyborg.Core.Aot.Modules.Validation.Rendering.Objects;
using Cyborg.Shared.Text;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Cyborg.Core.Aot.Modules.Validation.Rendering;

internal abstract class TextualValueSectionRenderer(ValidationContractInfo contractInfo, VisibilityContext visibilityContext, DiagnosticsReporter diagnosticsReporter)
    : SectionRenderer(contractInfo, visibilityContext, diagnosticsReporter)
{
    protected abstract string MethodName { get; }

    protected abstract bool ShouldSkip(PropertyModel property);

    protected abstract string CreateStringExpression(PropertyModel property, string accessExpression, string preparationKey);

    protected abstract string CreateElementExpression(ITypeSymbol elementType, string accessExpression, string preparationKey);

    public override void RenderSection(IndentedStringBuilder builder, ModuleModel model)
    {
        string qualifiedType = model.FullyQualifiedTypeName;
        builder.AppendBlock(
            $$"""
            private async {{KnownTypes.ValueTaskOfT(qualifiedType)}} {{MethodName}}(
                {{ContractInfo.ModuleValidationContext.RenderGlobal()}} {{ContextVariable}},
                {{KnownTypes.CancellationToken}} cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                {{qualifiedType}} {{RootModuleVariable}} = this;
            """);

        builder = builder.IncreaseIndent();
        AppendForObject(builder, model.Properties, RootModuleVariable, PreparationPath.Root(RootModuleVariable));
        builder = builder.DecreaseIndent();
        builder.AppendBlock(
            $$"""
                await {{KnownTypes.Task}}.CompletedTask;
                return {{RootModuleVariable}};
            }
            """);
    }

    private bool AppendForObject(IndentedStringBuilder builder, ImmutableArray<PropertyModel> properties, string targetVariable, PreparationPath path)
    {
        List<(string PropertyName, string LocalName)> assignments = [];

        foreach (PropertyModel property in properties)
        {
            string propertyAccess = $"{targetVariable}.{property.Name}";
            string localName = $"{targetVariable}_{property.Name}";
            PreparationPath propertyPath = path.AppendProperty(property.Name);
            bool isStringLike = property.Symbol.Type.IsStringLike(ContractInfo.TaggedString);
            bool skipProperty = isStringLike && ShouldSkip(property);
            bool hasNestedWork = property.Object is { HasChildren: true } objectModel && HasWork(objectModel.Children);
            CollectionModel? collection = property.Collection;
            bool hasCollectionWork = collection is { Shape.SupportsElementRewrite: true }
                && (collection.ElementType.IsStringLike(ContractInfo.TaggedString)
                    || (collection.ElementObject is { } elementObject && HasWork(elementObject.Children)));

            if (!hasNestedWork && (!isStringLike && !hasCollectionWork || skipProperty))
            {
                continue;
            }
            if (property.Symbol.SetMethod is not { } setter || !VisibilityContext.IsVisible(setter))
            {
                continue;
            }

            if (isStringLike)
            {
                EmitStringRewrite(builder, property, localName, propertyAccess, propertyPath);
            }
            else
            {
                builder.AppendLine($"{property.NullableTypeName} {localName} = {propertyAccess};");
                if (hasNestedWork)
                {
                    AppendNestedRewrite(builder, property, localName, propertyPath);
                }
                if (hasCollectionWork)
                {
                    AppendCollectionRewrite(builder, property, collection!, localName, propertyPath);
                }
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

    private void EmitStringRewrite(IndentedStringBuilder builder, PropertyModel property, string localName, string propertyAccess, PreparationPath preparationPath)
    {
        string rewrittenExpression = CreateStringExpression(property, propertyAccess, preparationPath.RenderExpression());
        if (!property.Symbol.Type.CanEverBeNull)
        {
            builder.AppendLine($"{property.NullableTypeName} {localName} = {rewrittenExpression};");
            return;
        }

        if (property.IsNullable)
        {
            builder.AppendLine($"{property.NullableTypeName} {localName} = {propertyAccess} is not null ? {rewrittenExpression} : null;");
        }
        else
        {
            builder.AppendLine($"{property.NullableTypeName} {localName} = {propertyAccess} is not null ? {rewrittenExpression} : {propertyAccess}!;");
        }
    }

    private void AppendNestedRewrite(IndentedStringBuilder builder, PropertyModel property, string localName, PreparationPath preparationPath)
    {
        ObjectModel objectModel = property.Object
            ?? throw new InvalidOperationException($"Nested textual value preparation requires object metadata for property '{property.Name}'.");
        string nestedVariable = $"{localName}Current";

        objectModel.Renderer.AppendRewrite(
            builder,
            localName,
            nestedVariable,
            (nestedBuilder, currentVariable) => AppendForObject(nestedBuilder, objectModel.Children, currentVariable, preparationPath));
    }

    private void AppendCollectionRewrite(IndentedStringBuilder builder, PropertyModel property, CollectionModel collection, string localName, PreparationPath preparationPath)
    {
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
            AppendCollectionRewriteBody(builder.IncreaseIndent(), collection, collectionCurrentVariable, preparationPath);
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

        AppendCollectionRewriteBody(builder, collection, localName, preparationPath);
    }

    private void AppendCollectionRewriteBody(IndentedStringBuilder builder, CollectionModel collection, string collectionVariable, PreparationPath preparationPath)
    {
        string safeIdentifier = CreateSafeIdentifier(collectionVariable);
        string rewrittenItemsVariable = $"{safeIdentifier}Items";
        string elementVariable = $"{safeIdentifier}Element";
        string elementCurrentVariable = $"{safeIdentifier}ElementCurrent";
        string elementValueVariable = $"{safeIdentifier}ElementValue";
        string elementIndexVariable = $"{safeIdentifier}Index";
        PreparationPath elementPath = preparationPath.AppendIndex(elementIndexVariable);

        builder.AppendBlock(
            $$"""
            {{KnownTypes.ListOfT(collection.ElementNullableTypeName)}} {{rewrittenItemsVariable}} = [];
            int {{elementIndexVariable}} = 0;
            foreach ({{collection.ElementNullableTypeName}} {{elementVariable}} in {{collectionVariable}})
            {
            """);
        IndentedStringBuilder loopBuilder = builder.IncreaseIndent();

        if (collection.ElementType.IsStringLike(ContractInfo.TaggedString))
        {
            ValueAccess elementAccess = collection.Shape.Renderer.ElementAccess(elementVariable);
            if (elementAccess.RequiresGuard)
            {
                loopBuilder.AppendLine($"{collection.ElementNullableTypeName} {elementCurrentVariable} = {elementVariable};");
                elementAccess = collection.Shape.Renderer.ElementAccess(elementCurrentVariable);
                loopBuilder.AppendBlock(
                    $$"""
                    if ({{elementAccess.GuardExpression}})
                    {
                        {{collection.ElementNonNullableTypeName}} {{elementValueVariable}} = {{CreateElementExpression(collection.ElementType, elementAccess.ValueExpression, elementPath.RenderExpression())}};
                        {{elementCurrentVariable}} = {{elementValueVariable}};
                    }
                    """);
                loopBuilder.AppendLine($"{ModuleValidationRenderer.Helpers}.{ModuleValidationRenderer.HelperMembers.NullableRelax}({elementCurrentVariable});");
                loopBuilder.AppendLine($"{rewrittenItemsVariable}.Add({elementCurrentVariable});");
            }
            else
            {
                loopBuilder.AppendLine($"{collection.ElementNonNullableTypeName} {elementCurrentVariable} = {CreateElementExpression(collection.ElementType, elementAccess.ValueExpression, elementPath.RenderExpression())};");
                loopBuilder.AppendLine($"{rewrittenItemsVariable}.Add({elementCurrentVariable});");
            }
        }
        else
        {
            ObjectModel elementObject = collection.ElementObject
                ?? throw new InvalidOperationException("Collection element textual value preparation requires validatable object metadata.");
            loopBuilder.AppendLine($"{collection.ElementNullableTypeName} {elementCurrentVariable} = {elementVariable};");
            elementObject.Renderer.AppendRewrite(
                loopBuilder,
                elementCurrentVariable,
                elementValueVariable,
                (elementBuilder, currentVariable) => AppendForObject(elementBuilder, elementObject.Children, currentVariable, elementPath));
            loopBuilder.AppendLine($"{rewrittenItemsVariable}.Add({elementCurrentVariable});");
        }

        loopBuilder.AppendLine($"++{elementIndexVariable};");
        builder.AppendLine("}");
        collection.Renderer.AppendMaterialization(builder, collectionVariable, rewrittenItemsVariable);
    }

    private bool HasWork(ImmutableArray<PropertyModel> properties)
    {
        foreach (PropertyModel property in properties)
        {
            if (property.Symbol.Type.IsStringLike(ContractInfo.TaggedString) && !ShouldSkip(property))
            {
                return true;
            }
            if (property.Object is { HasChildren: true } objectModel && HasWork(objectModel.Children))
            {
                return true;
            }
            if (property.Collection is { Shape.SupportsElementRewrite: true } collection)
            {
                if (collection.ElementType.IsStringLike(ContractInfo.TaggedString))
                {
                    return true;
                }
                if (collection.ElementObject is { } elementObject && HasWork(elementObject.Children))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static string CreateSafeIdentifier(string value) =>
        string.Concat(value.Select(static character => char.IsLetterOrDigit(character) ? character : '_'));
}
