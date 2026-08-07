using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Models;
using Microsoft.CodeAnalysis.CSharp;

namespace Cyborg.Core.Aot.Modules.Validation.Rendering;

/// <summary>
/// Emits module identity plus a format-neutral recursive description.
/// </summary>
internal sealed class InspectionSectionRenderer(
    ValidationContractInfo contractInfo,
    DiagnosticsReporter diagnosticsReporter) : ISectionRenderer
{
    public void RenderSection(IndentedStringBuilder builder, ModuleModel model)
    {
        builder.AppendLine(
            $"public override string ToString() => {contractInfo.ModuleIdentity.RenderGlobal()}.Format(ModuleId, Name, Group);");
        builder.AppendLine();
        builder.AppendLine(
            $"public {KnownTypes.ValueTask} DescribeAsync(");
        builder.IncreaseIndent().AppendLine(
            $"{contractInfo.IObjectDescriptionBuilder.RenderGlobal()} builder,");
        builder.IncreaseIndent().AppendLine(
            $"{KnownTypes.CancellationToken} cancellationToken)");
        builder.AppendLine("{");

        IndentedStringBuilder body = builder.IncreaseIndent();
        body.AppendLine($"{KnownTypes.ArgumentNullException}.ThrowIfNull(builder);");
        body.AppendLine("cancellationToken.ThrowIfCancellationRequested();");
        body.AppendLine("builder.AddProperty(\"ModuleId\", [], ModuleId);");

        foreach (PropertyModel property in model.Properties)
        {
            AppendNode(
                body,
                property,
                builderVariableName: "builder",
                nodeAccessExpression: property.Name,
                displayName: property.Name,
                symbolPath: property.Name,
                isProperty: true);
        }

        body.AppendLine($"return {KnownTypes.ValueTask}.CompletedTask;");
        builder.AppendLine("}");
    }

    private void AppendNode(
        IndentedStringBuilder builder,
        PropertyModel property,
        string builderVariableName,
        string nodeAccessExpression,
        string displayName,
        string symbolPath,
        bool isProperty)
    {
        string hintsExpression = CreateHintsExpression(property);

        if (property.Collection is not null)
        {
            AppendCollection(
                builder,
                property,
                builderVariableName,
                nodeAccessExpression,
                displayName,
                symbolPath,
                hintsExpression,
                isProperty);
        }
        else if (property.HasValidatableChildren)
        {
            AppendObject(
                builder,
                property,
                builderVariableName,
                nodeAccessExpression,
                displayName,
                symbolPath,
                hintsExpression,
                isProperty);
        }
        else
        {
            AppendAtom(
                builder,
                builderVariableName,
                nodeAccessExpression,
                displayName,
                hintsExpression,
                isProperty);
        }
    }

    private static void AppendAtom(
        IndentedStringBuilder builder,
        string builderVariableName,
        string nodeAccessExpression,
        string displayName,
        string hintsExpression,
        bool isProperty)
    {
        builder.AppendLine(
            isProperty
                ? $"{builderVariableName}.AddProperty(\"{displayName}\", {hintsExpression}, {nodeAccessExpression});"
                : $"{builderVariableName}.AddItem({hintsExpression}, {nodeAccessExpression});");
    }

    private void AppendObject(
        IndentedStringBuilder builder,
        PropertyModel property,
        string builderVariableName,
        string nodeAccessExpression,
        string displayName,
        string symbolPath,
        string hintsExpression,
        bool isProperty)
    {
        string childBuilderName = CreateSymbolName(
            $"{symbolPath}_object_description_builder");
        bool requiresNullCheck =
            property.IsNullable || !property.Symbol.Type.IsValueType;
        string objectAccessExpression =
            property.IsNullable && property.Symbol.Type.IsValueType
                ? $"{nodeAccessExpression}.Value"
                : nodeAccessExpression;

        if (!requiresNullCheck)
        {
            AppendObjectBody(
                builder,
                property,
                builderVariableName,
                objectAccessExpression,
                displayName,
                symbolPath,
                hintsExpression,
                isProperty,
                childBuilderName);
            return;
        }

        builder.AppendLine($"if ({nodeAccessExpression} is null)");
        builder.AppendLine("{");
        AppendAtom(
            builder.IncreaseIndent(),
            builderVariableName,
            nodeAccessExpression,
            displayName,
            hintsExpression,
            isProperty);
        builder.AppendLine("}");
        builder.AppendLine("else");
        builder.AppendLine("{");
        AppendObjectBody(
            builder.IncreaseIndent(),
            property,
            builderVariableName,
            objectAccessExpression,
            displayName,
            symbolPath,
            hintsExpression,
            isProperty,
            childBuilderName);
        builder.AppendLine("}");
    }

    private void AppendObjectBody(
        IndentedStringBuilder builder,
        PropertyModel property,
        string builderVariableName,
        string objectAccessExpression,
        string displayName,
        string symbolPath,
        string hintsExpression,
        bool isProperty,
        string childBuilderName)
    {
        builder.AppendLine(
            isProperty
                ? $"{builderVariableName}.AddObject(\"{displayName}\", {hintsExpression}, {childBuilderName} =>"
                : $"{builderVariableName}.AddObjectItem({hintsExpression}, {childBuilderName} =>");
        builder.AppendLine("{");

        IndentedStringBuilder childBody = builder.IncreaseIndent();
        foreach (PropertyModel child in property.Children)
        {
            AppendNode(
                childBody,
                child,
                childBuilderName,
                $"{objectAccessExpression}.{child.Name}",
                child.Name,
                $"{symbolPath}_{child.Name}",
                isProperty: true);
        }

        builder.AppendLine("});");
    }

    private void AppendCollection(
        IndentedStringBuilder builder,
        PropertyModel property,
        string builderVariableName,
        string nodeAccessExpression,
        string displayName,
        string symbolPath,
        string hintsExpression,
        bool isProperty)
    {
        bool hasGuard = CollectionHelpers.TryConstructEnumerationGuardExpression(
            property,
            nodeAccessExpression,
            out string? guardExpression,
            out string valueExpression);

        if (!hasGuard)
        {
            AppendCollectionBody(
                builder,
                property,
                builderVariableName,
                valueExpression,
                displayName,
                symbolPath,
                hintsExpression,
                isProperty);
            return;
        }

        builder.AppendLine($"if ({guardExpression})");
        builder.AppendLine("{");
        AppendCollectionBody(
            builder.IncreaseIndent(),
            property,
            builderVariableName,
            valueExpression,
            displayName,
            symbolPath,
            hintsExpression,
            isProperty);
        builder.AppendLine("}");
        builder.AppendLine("else");
        builder.AppendLine("{");
        AppendAtom(
            builder.IncreaseIndent(),
            builderVariableName,
            nodeAccessExpression,
            displayName,
            hintsExpression,
            isProperty);
        builder.AppendLine("}");
    }

    private void AppendCollectionBody(
        IndentedStringBuilder builder,
        PropertyModel property,
        string builderVariableName,
        string collectionAccessExpression,
        string displayName,
        string symbolPath,
        string hintsExpression,
        bool isProperty)
    {
        CollectionModel collection = property.Collection!;
        string collectionBuilderName = CreateSymbolName(
            $"{symbolPath}_collection_description_builder");
        string elementName = CreateSymbolName(
            $"{symbolPath}_description_element");

        builder.AppendLine(
            isProperty
                ? $"{builderVariableName}.AddCollection(\"{displayName}\", {hintsExpression}, {collectionBuilderName} =>"
                : $"{builderVariableName}.AddCollectionItem({hintsExpression}, {collectionBuilderName} =>");
        builder.AppendLine("{");

        IndentedStringBuilder collectionBody = builder.IncreaseIndent();
        collectionBody.AppendLine(
            $"foreach ({collection.ElementNullableTypeName} {elementName} in {collectionAccessExpression})");
        collectionBody.AppendLine("{");

        IndentedStringBuilder elementBody = collectionBody.IncreaseIndent();
        elementBody.AppendLine("cancellationToken.ThrowIfCancellationRequested();");
        if (collection.IsElementValidatableType
            && !collection.ElementChildren.IsDefaultOrEmpty)
        {
            AppendCollectionObjectElement(
                elementBody,
                collection,
                collectionBuilderName,
                elementName,
                symbolPath);
        }
        else
        {
            elementBody.AppendLine(
                $"{collectionBuilderName}.AddItem([], {elementName});");
        }

        collectionBody.AppendLine("}");
        builder.AppendLine("});");
    }

    private void AppendCollectionObjectElement(
        IndentedStringBuilder builder,
        CollectionModel collection,
        string collectionBuilderName,
        string elementName,
        string symbolPath)
    {
        string elementBuilderName = CreateSymbolName(
            $"{symbolPath}_element_description_builder");
        string elementAccessExpression =
            collection.IsElementNullable && collection.ElementType.IsValueType
                ? $"{elementName}.Value"
                : elementName;

        if (collection.ElementRequiresNullCheck)
        {
            builder.AppendLine($"if ({elementName} is null)");
            builder.AppendLine("{");
            builder.IncreaseIndent().AppendLine(
                $"{collectionBuilderName}.AddItem([], {elementName});");
            builder.AppendLine("}");
            builder.AppendLine("else");
            builder.AppendLine("{");

            IndentedStringBuilder objectBody = builder.IncreaseIndent();
            objectBody.AppendLine(
                $"{collectionBuilderName}.AddObjectItem([], {elementBuilderName} =>");
            objectBody.AppendLine("{");
            AppendCollectionElementProperties(
                objectBody.IncreaseIndent(),
                collection,
                elementBuilderName,
                elementAccessExpression,
                symbolPath);
            objectBody.AppendLine("});");
            builder.AppendLine("}");
            return;
        }

        builder.AppendLine(
            $"{collectionBuilderName}.AddObjectItem([], {elementBuilderName} =>");
        builder.AppendLine("{");
        AppendCollectionElementProperties(
            builder.IncreaseIndent(),
            collection,
            elementBuilderName,
            elementAccessExpression,
            symbolPath);
        builder.AppendLine("});");
    }

    private void AppendCollectionElementProperties(
        IndentedStringBuilder builder,
        CollectionModel collection,
        string elementBuilderName,
        string elementAccessExpression,
        string symbolPath)
    {
        foreach (PropertyModel child in collection.ElementChildren)
        {
            AppendNode(
                builder,
                child,
                elementBuilderName,
                $"{elementAccessExpression}.{child.Name}",
                child.Name,
                $"{symbolPath}_element_{child.Name}",
                isProperty: true);
        }
    }

    private string CreateHintsExpression(PropertyModel property)
    {
        List<string> hints = [];
        foreach (PropertyAspect aspect in property.Aspects)
        {
            aspect.RegisterDescriptorHints(
                hints,
                diagnosticsReporter,
                property);
        }

        return hints.Count == 0
            ? "[]"
            : $"[{string.Join(", ", hints.Select(static hint => SymbolDisplay.FormatLiteral(hint, quote: true)))}]";
    }

    private static string CreateSymbolName(string path) =>
        SymbolNameGenerator.MakeCamelCase(path);
}
