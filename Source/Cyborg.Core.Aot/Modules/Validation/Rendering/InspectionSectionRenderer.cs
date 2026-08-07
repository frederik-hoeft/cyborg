using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Cyborg.Core.Aot.Modules.Validation.Rendering;

/// <summary>
/// Emits module identity plus a format-neutral recursive description.
/// </summary>
internal sealed class InspectionSectionRenderer(ValidationContractInfo contractInfo, VisibilityContext visibilityContext, DiagnosticsReporter diagnosticsReporter)
    : SectionRenderer(contractInfo, visibilityContext, diagnosticsReporter)
{
    public override void RenderSection(IndentedStringBuilder builder, ModuleModel model)
    {
        builder.AppendBlock(
            $$"""
            public override string ToString() => {{ContractInfo.ModuleIdentity.RenderGlobal()}}.Format(ModuleId, Name, Group);

            public {{KnownTypes.ValueTask}} DescribeAsync(
                {{ContractInfo.IObjectDescriptionBuilder.RenderGlobal()}} builder,
                {{KnownTypes.CancellationToken}} cancellationToken)
            {
                {{KnownTypes.ArgumentNullException}}.ThrowIfNull(builder);
                cancellationToken.ThrowIfCancellationRequested();

                builder.AddProperty("ModuleId", [], ModuleId);
            """);

        IndentedStringBuilder body = builder.IncreaseIndent();
        foreach (PropertyModel property in model.Properties)
        {
            AppendNode(body, property, builderVariableName: "builder", nodeAccessExpression: property.Name, displayName: property.Name, symbolPath: property.Name, isProperty: true);
        }
        builder.AppendBlock(
            $$"""
                return {{KnownTypes.ValueTask}}.CompletedTask;
            }
            """);
    }

    private void AppendNode(IndentedStringBuilder builder, PropertyModel property, string builderVariableName, string nodeAccessExpression, string displayName, string symbolPath, bool isProperty)
    {
        string hintsExpression = CreateHintsExpression(property);

        if (property.Collection is not null)
        {
            AppendCollection(builder, property, builderVariableName, nodeAccessExpression, displayName, symbolPath, hintsExpression, isProperty);
        }
        else if (property.HasValidatableChildren)
        {
            AppendObject(builder, property, builderVariableName, nodeAccessExpression, displayName, symbolPath, hintsExpression, isProperty);
        }
        else
        {
            AppendAtom(builder, builderVariableName, nodeAccessExpression, displayName, hintsExpression, isProperty);
        }
    }

    private static void AppendAtom(IndentedStringBuilder builder, string builderVariableName, string nodeAccessExpression, string displayName, string hintsExpression, bool isProperty)
    {
        builder.AppendLine(isProperty
            ? $"{builderVariableName}.AddProperty(\"{displayName}\", {hintsExpression}, {nodeAccessExpression});"
            : $"{builderVariableName}.AddItem({hintsExpression}, {nodeAccessExpression});");
    }

    private void AppendObject(IndentedStringBuilder builder, PropertyModel property, string builderVariableName, string nodeAccessExpression, string displayName,
        string symbolPath, string hintsExpression, bool isProperty)
    {
        string childBuilderName = SymbolNameGenerator.MakeCamelCase($"{symbolPath}Builder");
        bool requiresNullCheck = property.IsNullable || !property.Symbol.Type.IsValueType;
        string objectAccessExpression = property.IsNullable && property.Symbol.Type.IsValueType
            ? $"{nodeAccessExpression}.Value"
            : nodeAccessExpression;

        if (!requiresNullCheck)
        {
            AppendObjectBody(builder, property, builderVariableName, objectAccessExpression, displayName, symbolPath, hintsExpression, isProperty, childBuilderName);
            return;
        }

        builder.AppendBlock($$"""
            if ({{nodeAccessExpression}} is null)
            {
            """);
        AppendAtom(builder.IncreaseIndent(), builderVariableName, nodeAccessExpression, displayName, hintsExpression, isProperty);
        builder.AppendBlock($$"""
            }
            else
            {
            """);
        AppendObjectBody(builder.IncreaseIndent(), property, builderVariableName, objectAccessExpression, displayName, symbolPath, hintsExpression, isProperty, childBuilderName);
        builder.AppendLine("}");
    }

    private void AppendObjectBody(IndentedStringBuilder builder, PropertyModel property, string builderVariableName, string objectAccessExpression, string displayName,
        string symbolPath, string hintsExpression, bool isProperty, string childBuilderName)
    {
        string invocation = isProperty
            ? $"{builderVariableName}.AddObject(\"{displayName}\", {hintsExpression}, {childBuilderName} =>"
            : $"{builderVariableName}.AddObjectItem({hintsExpression}, {childBuilderName} =>";
        builder.AppendBlock(
            $$"""
            {{invocation}}
            {
            """);

        IndentedStringBuilder childBody = builder.IncreaseIndent();
        foreach (PropertyModel child in property.Children)
        {
            AppendNode(childBody, child, childBuilderName, nodeAccessExpression: $"{objectAccessExpression}.{child.Name}", child.Name, symbolPath: $"{symbolPath}_{child.Name}", isProperty: true);
        }

        builder.AppendLine("});");
    }

    private void AppendCollection(IndentedStringBuilder builder, PropertyModel property, string builderVariableName, string nodeAccessExpression, string displayName,
        string symbolPath, string hintsExpression, bool isProperty)
    {
        if (!CollectionHelpers.TryConstructEnumerationGuardExpression(property, nodeAccessExpression, out string? guardExpression, out string valueExpression))
        {
            AppendCollectionBody(builder, property, builderVariableName, valueExpression, displayName, symbolPath, hintsExpression, isProperty);
            return;
        }

        builder.AppendBlock(
            $$"""
            if ({{guardExpression}})
            {
            """);
        AppendCollectionBody(builder.IncreaseIndent(), property, builderVariableName, valueExpression, displayName, symbolPath, hintsExpression, isProperty);
        builder.AppendBlock($$"""
            }
            else
            {
            """);
        AppendAtom(builder.IncreaseIndent(), builderVariableName, nodeAccessExpression, displayName, hintsExpression, isProperty);
        builder.AppendLine("}");
    }

    private void AppendCollectionBody(IndentedStringBuilder builder, PropertyModel property, string builderVariableName, string collectionAccessExpression,
        string displayName, string symbolPath, string hintsExpression, bool isProperty)
    {
        CollectionModel collection = property.Collection!;
        string collectionBuilderName = SymbolNameGenerator.MakeCamelCase($"{symbolPath}Builder");
        string elementName = SymbolNameGenerator.MakeCamelCase($"{symbolPath}Element");

        string invocation = isProperty
            ? $"{builderVariableName}.AddCollection(\"{displayName}\", {hintsExpression}, {collectionBuilderName} =>"
            : $"{builderVariableName}.AddCollectionItem({hintsExpression}, {collectionBuilderName} =>";
        builder.AppendBlock(
            $$"""
            {{invocation}}
            {
                foreach ({{collection.ElementNullableTypeName}} {{elementName}} in {{collectionAccessExpression}})
                {
                    cancellationToken.ThrowIfCancellationRequested();
            """);
        IndentedStringBuilder elementBody = builder.IncreaseIndent(levels: 2);
        if (collection.IsElementValidatableType && !collection.ElementChildren.IsDefaultOrEmpty)
        {
            AppendCollectionObjectElement(elementBody, collection, collectionBuilderName, elementName, symbolPath);
        }
        else
        {
            elementBody.AppendLine($"{collectionBuilderName}.AddItem([], {elementName});");
        }
        builder.AppendBlock(
            $$"""
                }
            });
            """);
    }

    private void AppendCollectionObjectElement(IndentedStringBuilder builder, CollectionModel collection, string collectionBuilderName, string elementName, string symbolPath)
    {
        string elementBuilderName = SymbolNameGenerator.MakeCamelCase($"{symbolPath}ElementBuilder");
        string elementAccessExpression = collection.IsElementNullable && collection.ElementType.IsValueType
            ? $"{elementName}.Value"
            : elementName;

        if (collection.ElementRequiresNullCheck)
        {
            builder.AppendBlock(
                $$"""
                if ({{elementName}} is null)
                {
                    {{collectionBuilderName}}.AddItem([], {{elementName}});
                }
                else
                {
                    {{collectionBuilderName}}.AddObjectItem([], {{elementBuilderName}} =>
                    {
                """);

            AppendCollectionElementProperties(builder.IncreaseIndent(levels: 2), collection, elementBuilderName, elementAccessExpression, symbolPath);
            builder.AppendBlock(
                $$"""
                    });
                }
                """);
            return;
        }

        builder.AppendBlock(
            $$"""
            {{collectionBuilderName}}.AddObjectItem([], {{elementBuilderName}} =>
            {
            """);
        AppendCollectionElementProperties(builder.IncreaseIndent(), collection, elementBuilderName, elementAccessExpression, symbolPath);
        builder.AppendLine("});");
    }

    private void AppendCollectionElementProperties(IndentedStringBuilder builder, CollectionModel collection, string elementBuilderName, string elementAccessExpression, string symbolPath)
    {
        foreach (PropertyModel child in collection.ElementChildren)
        {
            AppendNode(
                builder,
                property: child,
                builderVariableName: elementBuilderName,
                nodeAccessExpression: $"{elementAccessExpression}.{child.Name}",
                displayName: child.Name,
                symbolPath: $"{symbolPath}_Element_{child.Name}",
                isProperty: true);
        }
    }

    private string CreateHintsExpression(PropertyModel property)
    {
        List<string> hints = [];
        foreach (PropertyAspect aspect in property.Aspects)
        {
            aspect.RegisterDescriptorHints(hints, DiagnosticsReporter, property);
        }

        return hints.Count == 0 ? "[]" : $"[{string.Join(", ", hints.Select(static hint => SymbolDisplay.FormatLiteral(hint, quote: true)))}]";
    }
}
