using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Models;
using Cyborg.Core.Aot.Modules.Validation.Processors;
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

            bool isStringLike = TypeSymbolHelpers.IsStringLikeType(property.Symbol.Type);
            bool hasSecret = property.HasAspect<SecretAspect>();
            bool ignoreInterpolation = property.HasAspect<IgnoreInterpolationAspect>();
            bool hasNestedWork = property.HasValidatableChildren && HasInterpolationWork(property.Children);
            CollectionModel? collection = property.Collection;
            bool hasCollectionWork = collection is { SupportsElementRewrite: true }
                && (TypeSymbolHelpers.IsStringLikeType(collection.ElementType)
                    || (collection.IsElementValidatableType && HasInterpolationWork(collection.ElementChildren)));

            if (!hasNestedWork && !hasSecret && (!isStringLike && !hasCollectionWork || ignoreInterpolation))
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
                EmitStringInterpolation(builder, property, localName, propertyAccess, interpolate: !ignoreInterpolation);
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

    private void EmitStringInterpolation(IndentedStringBuilder builder, PropertyModel property, string localName, string propertyAccess, bool interpolate)
    {
        bool isTaggedString = TypeSymbolHelpers.IsTaggedString(property.Symbol.Type);
        string interpolatedExpression = interpolate
            ? $"{ContextVariable}.Interpolate({propertyAccess})"
            : propertyAccess;
        PropertyRewriteContext rewriteContext = new(property, ContractInfo, DiagnosticsReporter, RootModuleVariable, ContextVariable, propertyAccess);
        foreach (PropertyAspect aspect in property.Aspects)
        {
            interpolatedExpression = aspect.RewriteInterpolationExpression(rewriteContext, interpolatedExpression);
        }
        if (!isTaggedString)
        {
            interpolatedExpression = $"{interpolatedExpression}.Value";
        }

        if (!TypeSymbolHelpers.RequiresNullGuard(property.Symbol.Type))
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
        string nestedVar = $"{localName}Current";

        if (property.IsNullable || !property.HasDefault)
        {
            builder.AppendBlock(
                $$"""
                if ({{localName}} is not null)
                {
                    {{property.NonNullableTypeName}} {{nestedVar}} = {{localName}};
                """);
            AppendInterpolationForObject(builder.IncreaseIndent(), property.Children, nestedVar);
            builder.AppendBlock(
                $$"""
                    {{localName}} = {{nestedVar}};
                }
                """);
            if (!property.IsNullable)
            {
                builder.AppendLine($"{ModuleValidationRenderer.Helpers}.{ModuleValidationRenderer.HelperMembers.NullableRelax}({localName});");
            }
            return;
        }

        builder.AppendLine($"{property.NonNullableTypeName} {nestedVar} = {localName};");
        AppendInterpolationForObject(builder, property.Children, nestedVar);
        builder.AppendLine($"{localName} = {nestedVar};");
    }

    private void AppendCollectionInterpolation(IndentedStringBuilder builder, PropertyModel property, CollectionModel collection, string localName)
    {
        if (CollectionHelpers.TryConstructEnumerationGuardExpression(property, localName, out string? conditionExpression, out string valueExpression))
        {
            string collectionCurrentVar = $"{localName}Current";
            builder.AppendBlock(
                $$"""
                if ({{conditionExpression}})
                {
                    {{property.NonNullableTypeName}} {{collectionCurrentVar}} = {{valueExpression}};
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

        bool isStringElem = TypeSymbolHelpers.IsStringLikeType(collection.ElementType);

        if (collection.ElementRequiresNullCheck)
        {
            loopBuilder.AppendLine($"{collection.ElementNullableTypeName} {elemCurrentVar} = {elemVar};");
            loopBuilder.AppendBlock(
                $$"""
                if ({{elemCurrentVar}} is not null)
                {
                """);
            IndentedStringBuilder ifBuilder = loopBuilder.IncreaseIndent();
            if (isStringElem)
            {
                ifBuilder.AppendLine($"{collection.ElementNonNullableTypeName} {elemValueVar} = {CreateElementInterpolationExpression(collection.ElementType, $"{elemCurrentVar}!")};");
                ifBuilder.AppendLine($"{elemCurrentVar} = {elemValueVar};");
            }
            else
            {
                ifBuilder.AppendLine($"{collection.ElementNonNullableTypeName} {elemValueVar} = {elemCurrentVar}!;");
                AppendInterpolationForObject(ifBuilder, collection.ElementChildren, elemValueVar);
                ifBuilder.AppendLine($"{elemCurrentVar} = {elemValueVar};");
            }
            loopBuilder.AppendBlock(
                $$"""
                }
                {{ModuleValidationRenderer.Helpers}}.{{ModuleValidationRenderer.HelperMembers.NullableRelax}}({{elemCurrentVar}});
                {{rewrittenItemsVar}}.Add({{elemCurrentVar}});
                """);
        }
        else
        {
            if (isStringElem)
            {
                loopBuilder.AppendLine($"{collection.ElementNonNullableTypeName} {elemCurrentVar} = {CreateElementInterpolationExpression(collection.ElementType, elemVar)};");
            }
            else
            {
                loopBuilder.AppendLine($"{collection.ElementNonNullableTypeName} {elemCurrentVar} = {elemVar};");
                AppendInterpolationForObject(loopBuilder, collection.ElementChildren, elemCurrentVar);
            }
            loopBuilder.AppendLine($"{rewrittenItemsVar}.Add({elemCurrentVar});");
        }

        builder.AppendLine("}");
        AppendCollectionMaterialization(builder, collection, collectionVar, rewrittenItemsVar);
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
                string safeId = CreateSafeIdentifier(targetVariable);
                string rewrittenCollectionVar = $"{safeId}Collection";
                string rewrittenCollectionItemsVar = $"{safeId}CollectionItems";
                string rewrittenItemVar = $"{safeId}Item";
                builder.AppendBlock(
                    $$"""
                    {{collection.MaterializationTypeName}} {{rewrittenCollectionVar}} = new();
                    {{KnownTypes.ICollectionOfT(collection.ElementNullableTypeName)}} {{rewrittenCollectionItemsVar}} = {{rewrittenCollectionVar}};
                    foreach ({{collection.ElementNullableTypeName}} {{rewrittenItemVar}} in {{rewrittenItemsVariable}})
                    {
                        {{rewrittenCollectionItemsVar}}.Add({{rewrittenItemVar}});
                    }
                    {{targetVariable}} = {{rewrittenCollectionVar}};
                    """);
                break;
        }
    }

    internal static bool HasInterpolationWork(ImmutableArray<PropertyModel> properties)
    {
        foreach (PropertyModel property in properties)
        {
            if (TypeSymbolHelpers.IsStringLikeType(property.Symbol.Type) || property.HasAspect<SecretAspect>())
            {
                return true;
            }
            if (property.HasValidatableChildren && HasInterpolationWork(property.Children))
            {
                return true;
            }
            if (property.Collection is { SupportsElementRewrite: true } collection)
            {
                if (TypeSymbolHelpers.IsStringLikeType(collection.ElementType))
                {
                    return true;
                }
                if (collection.IsElementValidatableType && HasInterpolationWork(collection.ElementChildren))
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
        return TypeSymbolHelpers.IsTaggedString(elementType) ? interpolated : $"{interpolated}.Value";
    }

    private static string CreateSafeIdentifier(string value) =>
        string.Concat(value.Select(static c => char.IsLetterOrDigit(c) ? c : '_'));
}
