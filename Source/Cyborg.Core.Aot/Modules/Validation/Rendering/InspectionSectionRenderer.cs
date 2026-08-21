using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Models;
using Cyborg.Core.Aot.Modules.Validation.Rendering.Objects;
using Cyborg.Core.Aot.Modules.Validation.Rendering.Collections;
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

            public override {{ContractInfo.IModuleDescriptor.RenderGlobal()}} GetDescriptor() => this;

            public {{KnownTypes.ValueTask}} DescribeAsync(
                {{ContractInfo.IObjectDescriptionBuilder.RenderGlobal()}} builder,
                {{KnownTypes.CancellationToken}} cancellationToken)
            {
                {{KnownTypes.ArgumentNullException}}.ThrowIfNull(builder);
                cancellationToken.ThrowIfCancellationRequested();

                builder.AddProperty("ModuleId", ModuleId);
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
        string? hintsExpression = CreateHintsExpression(property);

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

    private static void AppendAtom(IndentedStringBuilder builder, string builderVariableName, string nodeAccessExpression, string displayName, string? hintsExpression, bool isProperty)
    {
        builder.AppendLine((isProperty, hintsExpression) switch
        {
            (true, null) => $"{builderVariableName}.AddProperty(\"{displayName}\", {nodeAccessExpression});",
            (true, not null) => $"{builderVariableName}.AddProperty(\"{displayName}\", {nodeAccessExpression}, {hintsExpression});",
            (false, null) => $"{builderVariableName}.AddItem({nodeAccessExpression});",
            (false, not null) => $"{builderVariableName}.AddItem({nodeAccessExpression}, {hintsExpression});",
        });
    }

    private void AppendObject(IndentedStringBuilder builder, PropertyModel property, string builderVariableName, string nodeAccessExpression, string displayName,
        string symbolPath, string? hintsExpression, bool isProperty)
    {
        ObjectModel objectModel = property.Object
            ?? throw new InvalidOperationException($"Object inspection requires object metadata for property '{property.Name}'.");
        ValueAccess access = objectModel.Shape.Renderer.Access(nodeAccessExpression);
        string childBuilderName = SymbolNameGenerator.MakeCamelCase($"{symbolPath}Builder");

        if (!access.RequiresGuard)
        {
            AppendObjectBody(builder, objectModel, builderVariableName, access.ValueExpression, displayName, symbolPath, hintsExpression, isProperty, childBuilderName);
            return;
        }

        builder.AppendBlock($$"""
            if ({{access.MissingExpression}})
            {
            """);
        AppendAtom(builder.IncreaseIndent(), builderVariableName, nodeAccessExpression, displayName, hintsExpression, isProperty);
        builder.AppendBlock($$"""
            }
            else
            {
            """);
        AppendObjectBody(builder.IncreaseIndent(), objectModel, builderVariableName, access.ValueExpression, displayName, symbolPath, hintsExpression, isProperty, childBuilderName);
        builder.AppendLine("}");
    }

    private void AppendObjectBody(IndentedStringBuilder builder, ObjectModel objectModel, string builderVariableName, string objectAccessExpression, string displayName,
        string symbolPath, string? hintsExpression, bool isProperty, string childBuilderName)
    {
        string invocation = isProperty
            ? $"{builderVariableName}.AddObject(\"{displayName}\", {childBuilderName} =>"
            : $"{builderVariableName}.AddObjectItem({childBuilderName} =>";
        builder.AppendBlock(
            $$"""
            {{invocation}}
            {
            """);

        IndentedStringBuilder childBody = builder.IncreaseIndent();
        foreach (PropertyModel child in objectModel.Children)
        {
            AppendNode(childBody, child, childBuilderName, nodeAccessExpression: $"{objectAccessExpression}.{child.Name}", child.Name, symbolPath: $"{symbolPath}_{child.Name}", isProperty: true);
        }
        if (hintsExpression is not null)
        {
            builder.AppendLine($"}}, {hintsExpression});");
        }
        else
        {
            builder.AppendLine("});");
        }
    }

    private void AppendCollection(IndentedStringBuilder builder, PropertyModel property, string builderVariableName, string nodeAccessExpression, string displayName,
        string symbolPath, string? hintsExpression, bool isProperty)
    {
        CollectionModel collection = property.Collection!;
        ValueAccess access = collection.Shape.Renderer.Access(nodeAccessExpression);
        if (!access.RequiresGuard)
        {
            AppendCollectionBody(builder, property, builderVariableName, access.ValueExpression, displayName, symbolPath, hintsExpression, isProperty);
            return;
        }

        builder.AppendBlock(
            $$"""
            if ({{access.GuardExpression}})
            {
            """);
        AppendCollectionBody(builder.IncreaseIndent(), property, builderVariableName, access.ValueExpression, displayName, symbolPath, hintsExpression, isProperty);
        builder.AppendBlock($$"""
            }
            else
            {
            """);
        AppendAtom(builder.IncreaseIndent(), builderVariableName, nodeAccessExpression, displayName, hintsExpression, isProperty);
        builder.AppendLine("}");
    }

    private void AppendCollectionBody(IndentedStringBuilder builder, PropertyModel property, string builderVariableName, string collectionAccessExpression,
        string displayName, string symbolPath, string? hintsExpression, bool isProperty)
    {
        CollectionModel collection = property.Collection ?? throw new InvalidOperationException($"A collection model is required for property '{property.Name}' to render a collection body.");
        string collectionBuilderName = SymbolNameGenerator.MakeCamelCase($"{symbolPath}Builder");
        string elementName = SymbolNameGenerator.MakeCamelCase($"{symbolPath}Element");

        string invocation = isProperty
            ? $"{builderVariableName}.AddCollection(\"{displayName}\", {collectionBuilderName} =>"
            : $"{builderVariableName}.AddCollectionItem({collectionBuilderName} =>";
        builder.AppendBlock(
            $$"""
            {{invocation}}
            {
                foreach ({{collection.ElementNullableTypeName}} {{elementName}} in {{collectionAccessExpression}})
                {
                    cancellationToken.ThrowIfCancellationRequested();
            """);
        IndentedStringBuilder elementBody = builder.IncreaseIndent(levels: 2);
        if (collection.ElementObject is { HasChildren: true })
        {
            AppendCollectionObjectElement(elementBody, collection, collectionBuilderName, elementName, symbolPath);
        }
        else
        {
            elementBody.AppendLine($"{collectionBuilderName}.AddItem({elementName});");
        }
        builder.IncreaseIndent().AppendLine("}");
        if (hintsExpression is not null)
        {
            builder.AppendLine($"}}, {hintsExpression});");
        }
        else
        {
            builder.AppendLine("});");
        }
    }

    private void AppendCollectionObjectElement(IndentedStringBuilder builder, CollectionModel collection, string collectionBuilderName, string elementName, string symbolPath)
    {
        string elementBuilderName = SymbolNameGenerator.MakeCamelCase($"{symbolPath}ElementBuilder");
        ObjectModel elementObject = collection.ElementObject
            ?? throw new InvalidOperationException("Collection object inspection requires validatable element metadata.");
        ValueAccess elementAccess = elementObject.Shape.Renderer.Access(elementName);

        if (elementAccess.RequiresGuard)
        {
            builder.AppendBlock(
                $$"""
                if ({{elementAccess.MissingExpression}})
                {
                    {{collectionBuilderName}}.AddItem({{elementName}});
                }
                else
                {
                    {{collectionBuilderName}}.AddObjectItem({{elementBuilderName}} =>
                    {
                """);

            AppendCollectionElementProperties(builder.IncreaseIndent(levels: 2), elementObject, elementBuilderName, elementAccess.ValueExpression, symbolPath);
            builder.AppendBlock(
                $$"""
                    });
                }
                """);
            return;
        }

        builder.AppendBlock(
            $$"""
            {{collectionBuilderName}}.AddObjectItem({{elementBuilderName}} =>
            {
            """);
        AppendCollectionElementProperties(builder.IncreaseIndent(), elementObject, elementBuilderName, elementAccess.ValueExpression, symbolPath);
        builder.AppendLine("});");
    }

    private void AppendCollectionElementProperties(IndentedStringBuilder builder, ObjectModel elementObject, string elementBuilderName, string elementAccessExpression, string symbolPath)
    {
        foreach (PropertyModel child in elementObject.Children)
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

    private string? CreateHintsExpression(PropertyModel property)
    {
        List<string> hints = [];
        foreach (PropertyAspect aspect in property.Aspects)
        {
            aspect.RegisterDescriptorHints(hints, ContractInfo, DiagnosticsReporter, property);
        }

        return hints.Count == 0 ? null : $"[{string.Join(", ", hints.Select(static hint => SymbolDisplay.FormatLiteral(hint, quote: true)))}]";
    }
}
