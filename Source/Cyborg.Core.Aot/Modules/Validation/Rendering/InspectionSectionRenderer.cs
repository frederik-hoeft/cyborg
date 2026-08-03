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
    private const string MODULE_IDENTITY_TYPE =
        "global::Cyborg.Core.Modules.Debugging.ModuleIdentity";

    private const string MODULE_DESCRIPTION_TYPE =
        "global::Cyborg.Core.Modules.Descriptors.ModuleDescription";

    public void RenderSection(IndentedStringBuilder builder, ModuleModel model)
    {
        builder.AppendBlock(
            $$"""
            public override string ToString() => {{MODULE_IDENTITY_TYPE}}.Format(ModuleId, Name, Group);

            public void Describe({{contractInfo.IObjectDescriptionBuilder.RenderGlobal()}} builder)
            {
                {{KnownTypes.ArgumentNullException}}.ThrowIfNull(builder);
                builder.AddProperty("ModuleId", [], ModuleId);
            """);

        IndentedStringBuilder body = builder.IncreaseIndent();
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

        builder.AppendBlock(
            $$"""
            }

            public string Inspect() => {{MODULE_DESCRIPTION_TYPE}}.ToText(this);
            """);
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

        builder.AppendBlock(
            isProperty
                ? $$"""
                  if ({{nodeAccessExpression}} is null)
                  {
                      {{builderVariableName}}.AddProperty("{{displayName}}", {{hintsExpression}}, {{nodeAccessExpression}});
                  }
                  else
                  {
                      {{builderVariableName}}.AddObject("{{displayName}}", {{hintsExpression}}, {{childBuilderName}} =>
                      {
                  """
                : $$"""
                  if ({{nodeAccessExpression}} is null)
                  {
                      {{builderVariableName}}.AddItem({{hintsExpression}}, {{nodeAccessExpression}});
                  }
                  else
                  {
                      {{builderVariableName}}.AddObjectItem({{hintsExpression}}, {{childBuilderName}} =>
                      {
                  """);

        IndentedStringBuilder childBody = builder.IncreaseIndent(2);
        foreach (PropertyModel child in property.Children)
        {
            AppendNode(
                childBody,
                child,
                childBuilderName,
                $"{nodeAccessExpression}.{child.Name}",
                child.Name,
                $"{symbolPath}_{child.Name}",
                isProperty: true);
        }

        builder.AppendBlock(
            """
                    });
                }
            """);
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
        CollectionModel collection = property.Collection!;
        string collectionBuilderName = CreateSymbolName(
            $"{symbolPath}_collection_description_builder");
        string elementName = CreateSymbolName(
            $"{symbolPath}_description_element");

        bool requiresNullCheck =
            property.IsNullable || !property.Symbol.Type.IsValueType;

        if (requiresNullCheck)
        {
            builder.AppendBlock(
                isProperty
                    ? $$"""
                      if ({{nodeAccessExpression}} is null)
                      {
                          {{builderVariableName}}.AddProperty("{{displayName}}", {{hintsExpression}}, {{nodeAccessExpression}});
                      }
                      else
                      {
                          {{builderVariableName}}.AddCollection("{{displayName}}", {{hintsExpression}}, {{collectionBuilderName}} =>
                          {
                      """
                    : $$"""
                      if ({{nodeAccessExpression}} is null)
                      {
                          {{builderVariableName}}.AddItem({{hintsExpression}}, {{nodeAccessExpression}});
                      }
                      else
                      {
                          {{builderVariableName}}.AddCollectionItem({{hintsExpression}}, {{collectionBuilderName}} =>
                          {
                      """);
        }
        else
        {
            builder.AppendBlock(
                isProperty
                    ? $$"""
                      {{builderVariableName}}.AddCollection("{{displayName}}", {{hintsExpression}}, {{collectionBuilderName}} =>
                      {
                      """
                    : $$"""
                      {{builderVariableName}}.AddCollectionItem({{hintsExpression}}, {{collectionBuilderName}} =>
                      {
                      """);
        }

        IndentedStringBuilder loopBuilder =
            builder.IncreaseIndent(requiresNullCheck ? 2 : 1);

        loopBuilder.AppendBlock(
            $$"""
            foreach ({{collection.ElementNullableTypeName}} {{elementName}} in {{nodeAccessExpression}})
            {
            """);

        IndentedStringBuilder elementBody = loopBuilder.IncreaseIndent();

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

        loopBuilder.AppendLine("}");

        builder.AppendBlock(
            requiresNullCheck
                ? """
                      });
                  }
                  """
                : """
                  });
                  """);
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

            AppendCollectionElementProperties(
                builder.IncreaseIndent(2),
                collection,
                elementBuilderName,
                elementName,
                symbolPath);

            builder.AppendBlock(
                """
                        });
                    }
                """);
        }
        else
        {
            builder.AppendBlock(
                $$"""
                {{collectionBuilderName}}.AddObjectItem([], {{elementBuilderName}} =>
                {
                """);

            AppendCollectionElementProperties(
                builder.IncreaseIndent(),
                collection,
                elementBuilderName,
                elementName,
                symbolPath);

            builder.AppendLine("});");
        }
    }

    private void AppendCollectionElementProperties(
        IndentedStringBuilder builder,
        CollectionModel collection,
        string elementBuilderName,
        string elementName,
        string symbolPath)
    {
        foreach (PropertyModel child in collection.ElementChildren)
        {
            AppendNode(
                builder,
                child,
                elementBuilderName,
                $"{elementName}.{child.Name}",
                child.Name,
                $"{symbolPath}_element_{child.Name}",
                isProperty: true);
        }
    }

    private string CreateHintsExpression(PropertyModel property)
    {
        List<string> hints = [];
        foreach (PropertyValidationAspect aspect in property.Aspects)
        {
            aspect.RegisterDescriptorHints(
                hints,
                diagnosticsReporter,
                property);
        }

        return hints.Count == 0
            ? "[]"
            : $"[{string.Join(", ", hints.Select(static hint => SymbolDisplay.FormatLiteral(hint, quote: false)))}]";
    }

    private static string CreateSymbolName(string path) => SymbolNameGenerator.MakeCamelCase(path);
}
