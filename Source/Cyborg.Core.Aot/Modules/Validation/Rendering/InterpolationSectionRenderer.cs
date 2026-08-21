using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Models;
using Cyborg.Core.Aot.Modules.Validation.Processors;
using Cyborg.Core.Aot.Modules.Validation.Rendering.Collections;
using Cyborg.Core.Aot.Modules.Validation.Rendering.Objects;
using Cyborg.Shared.Text;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Cyborg.Core.Aot.Modules.Validation.Rendering;

internal sealed class InterpolationSectionRenderer(ValidationContractInfo contractInfo, VisibilityContext visibilityContext, DiagnosticsReporter diagnosticsReporter)
    : SectionRenderer(contractInfo, visibilityContext, diagnosticsReporter)
{
    public override void RenderSection(IndentedStringBuilder builder, ModuleModel model)
    {
        string qualifiedType = model.FullyQualifiedTypeName;
        builder.AppendBlock(
            $$"""
            private async {{KnownTypes.ValueTaskOfT(qualifiedType)}} {{ModuleValidationRenderer.ApplyInterpolationAsync}}(
                {{ContractInfo.ModuleValidationContext.RenderGlobal()}} {{ContextVariable}},
                {{KnownTypes.CancellationToken}} cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                {{qualifiedType}} {{RootModuleVariable}} = this;
            """);

        builder = builder.IncreaseIndent();
        AppendInterpolationForObject(builder, model.Properties, RootModuleVariable);
        builder = builder.DecreaseIndent();
        builder.AppendBlock(
            $$"""
                await {{KnownTypes.Task}}.CompletedTask;
                return {{RootModuleVariable}};
            }
            """);
    }

    // Emits rewrite statements and a `with` reassignment for targetVariable. Returns true if any
    // work was emitted.
    private bool AppendInterpolationForObject(IndentedStringBuilder builder, ImmutableArray<PropertyModel> properties, string targetVariable)
    {
        List<(string PropertyName, string LocalName)> assignments = [];

        foreach (PropertyModel property in properties)
        {
            string propertyAccess = $"{targetVariable}.{property.Name}";
            string localName = $"{targetVariable}_{property.Name}";

            bool isStringLike = property.Symbol.Type.IsStringLike(ContractInfo.TaggedString);
            bool ignoreInterpolation = property.HasAspect<IgnoreInterpolationAspect>();
            bool hasNestedWork = property.Object is { HasChildren: true } objectModel
                && HasInterpolationWork(objectModel.Children);
            CollectionModel? collection = property.Collection;
            bool hasCollectionWork = collection is { Shape.SupportsElementRewrite: true }
                && (collection.ElementType.IsStringLike(ContractInfo.TaggedString)
                    || (collection.ElementObject is { } elementObject && HasInterpolationWork(elementObject.Children)));

            if (!hasNestedWork && (!isStringLike && !hasCollectionWork || ignoreInterpolation))
            {
                continue;
            }

            // Skip properties that cannot be rewritten via a 'with' expression.
            if (property.Symbol.SetMethod is not { } setter || !VisibilityContext.IsVisible(setter))
            {
                continue;
            }

            if (isStringLike)
            {
                EmitStringInterpolation(builder, property, localName, propertyAccess);
            }
            else
            {
                builder.AppendLine($"{property.NullableTypeName} {localName} = {propertyAccess};");
                if (hasNestedWork)
                {
                    AppendNestedInterpolation(builder, property, localName);
                }
                if (hasCollectionWork)
                {
                    AppendCollectionInterpolation(builder, property, collection!, localName);
                }
            }

            assignments.Add((property.Name, localName));
        }

        if (assignments.Count == 0)
        {
            return false;
        }

        builder.AppendLine($"{targetVariable} = {targetVariable} with {{ {string.Join(", ", assignments.Select(static a => $"{a.PropertyName} = {a.LocalName}"))} }};");
        return true;
    }

    private void EmitStringInterpolation(IndentedStringBuilder builder, PropertyModel property, string localName, string propertyAccess)
    {
        bool isTaggedString = property.Symbol.Type.EqualsIgnoreNullability(ContractInfo.TaggedString);
        string interpolatedExpression = $"{ContextVariable}.Interpolate({propertyAccess})";
        if (!isTaggedString)
        {
            interpolatedExpression = $"{interpolatedExpression}.Value";
        }

        if (!property.Symbol.Type.CanEverBeNull)
        {
            builder.AppendLine($"{property.NullableTypeName} {localName} = {interpolatedExpression};");
            return;
        }

        if (property.IsNullable)
        {
            builder.AppendLine($"{property.NullableTypeName} {localName} = {propertyAccess} is not null ? {interpolatedExpression} : null;");
        }
        else
        {
            // Non-nullable: guard against null defensively (validation will catch it if it is null).
            // Use ! on the fallback so the ternary stays typed as non-nullable and avoids CS8600/CS8601.
            builder.AppendLine($"{property.NullableTypeName} {localName} = {propertyAccess} is not null ? {interpolatedExpression} : {propertyAccess}!;");
        }
    }

    private void AppendNestedInterpolation(IndentedStringBuilder builder, PropertyModel property, string localName)
    {
        ObjectModel objectModel = property.Object
            ?? throw new InvalidOperationException($"Nested interpolation requires object metadata for property '{property.Name}'.");
        string nestedVariable = $"{localName}Current";

        objectModel.Renderer.AppendRewrite(
            builder,
            localName,
            nestedVariable,
            (nestedBuilder, currentVariable) => AppendInterpolationForObject(nestedBuilder, objectModel.Children, currentVariable));
    }

    private void AppendCollectionInterpolation(IndentedStringBuilder builder, PropertyModel property, CollectionModel collection, string localName)
    {
        ValueAccess access = collection.Shape.Renderer.Access(localName);
        if (access.RequiresGuard)
        {
            string collectionCurrentVar = $"{localName}Current";
            builder.AppendBlock(
                $$"""
                if ({{access.GuardExpression}})
                {
                    {{property.NonNullableTypeName}} {{collectionCurrentVar}} = {{access.ValueExpression}};
                """);
            AppendCollectionInterpolationBody(builder.IncreaseIndent(), collection, collectionCurrentVar);
            builder.AppendBlock(
                $$"""
                    {{localName}} = {{collectionCurrentVar}};
                }
                """);
            if (!property.IsNullable)
            {
                builder.AppendLine($"{ModuleValidationRenderer.Helpers}.{ModuleValidationRenderer.HelperMembers.NullableRelax}({localName});");
            }
            return;
        }

        AppendCollectionInterpolationBody(builder, collection, localName);
    }

    private void AppendCollectionInterpolationBody(IndentedStringBuilder builder, CollectionModel collection, string collectionVar)
    {
        string safeId = CreateSafeIdentifier(collectionVar);
        string rewrittenItemsVar = $"{safeId}Items";
        string elemVar = $"{safeId}Element";
        string elemCurrentVar = $"{safeId}ElementCurrent";
        string elemValueVar = $"{safeId}ElementValue";

        builder.AppendBlock(
            $$"""
            {{KnownTypes.ListOfT(collection.ElementNullableTypeName)}} {{rewrittenItemsVar}} = [];
            foreach ({{collection.ElementNullableTypeName}} {{elemVar}} in {{collectionVar}})
            {
            """);
        IndentedStringBuilder loopBuilder = builder.IncreaseIndent();

        bool isStringElement = collection.ElementType.IsStringLike(ContractInfo.TaggedString);
        if (isStringElement)
        {
            ValueAccess elementAccess = collection.Shape.Renderer.ElementAccess(elemVar);
            if (elementAccess.RequiresGuard)
            {
                loopBuilder.AppendLine($"{collection.ElementNullableTypeName} {elemCurrentVar} = {elemVar};");
                elementAccess = collection.Shape.Renderer.ElementAccess(elemCurrentVar);
                loopBuilder.AppendBlock(
                    $$"""
                    if ({{elementAccess.GuardExpression}})
                    {
                        {{collection.ElementNonNullableTypeName}} {{elemValueVar}} = {{CreateElementInterpolationExpression(collection.ElementType, elementAccess.ValueExpression)}};
                        {{elemCurrentVar}} = {{elemValueVar}};
                    }
                    """);
                loopBuilder.AppendLine($"{ModuleValidationRenderer.Helpers}.{ModuleValidationRenderer.HelperMembers.NullableRelax}({elemCurrentVar});");
                loopBuilder.AppendLine($"{rewrittenItemsVar}.Add({elemCurrentVar});");
            }
            else
            {
                loopBuilder.AppendLine($"{collection.ElementNonNullableTypeName} {elemCurrentVar} = {CreateElementInterpolationExpression(collection.ElementType, elementAccess.ValueExpression)};");
                loopBuilder.AppendLine($"{rewrittenItemsVar}.Add({elemCurrentVar});");
            }
        }
        else
        {
            ObjectModel elementObject = collection.ElementObject
                ?? throw new InvalidOperationException("Collection element interpolation requires validatable object metadata.");
            loopBuilder.AppendLine($"{collection.ElementNullableTypeName} {elemCurrentVar} = {elemVar};");
            elementObject.Renderer.AppendRewrite(
                loopBuilder,
                elemCurrentVar,
                elemValueVar,
                (elementBuilder, currentVariable) => AppendInterpolationForObject(elementBuilder, elementObject.Children, currentVariable));
            loopBuilder.AppendLine($"{rewrittenItemsVar}.Add({elemCurrentVar});");
        }

        builder.AppendLine("}");
        collection.Renderer.AppendMaterialization(builder, collectionVar, rewrittenItemsVar);
    }

    private bool HasInterpolationWork(ImmutableArray<PropertyModel> properties)
    {
        foreach (PropertyModel property in properties)
        {
            if (property.Symbol.Type.IsStringLike(ContractInfo.TaggedString))
            {
                return true;
            }
            if (property.Object is { HasChildren: true } objectModel && HasInterpolationWork(objectModel.Children))
            {
                return true;
            }
            if (property.Collection is { Shape.SupportsElementRewrite: true } collection)
            {
                if (collection.ElementType.IsStringLike(ContractInfo.TaggedString))
                {
                    return true;
                }
                if (collection.ElementObject is { } elementObject && HasInterpolationWork(elementObject.Children))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private string CreateElementInterpolationExpression(ITypeSymbol elementType, string accessExpression)
    {
        string interpolated = $"{ContextVariable}.Interpolate({accessExpression})";
        return elementType.EqualsIgnoreNullability(ContractInfo.TaggedString) ? interpolated : $"{interpolated}.Value";
    }

    private static string CreateSafeIdentifier(string value) =>
        string.Concat(value.Select(static c => char.IsLetterOrDigit(c) ? c : '_'));
}
