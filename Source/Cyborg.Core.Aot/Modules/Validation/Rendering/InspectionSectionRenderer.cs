using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Models;

namespace Cyborg.Core.Aot.Modules.Validation.Rendering;

/// <summary>
/// Emits module identity plus a format-neutral recursive description.
/// </summary>
internal sealed class InspectionSectionRenderer(ValidationContractInfo contractInfo) : ISectionRenderer
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
                isProperty: true);
        }

        builder.AppendBlock(
            $$"""
            }

            public string Inspect() => {{MODULE_DESCRIPTION_TYPE}}.ToText(this);
            """);
    }

    private static void AppendNode(
        IndentedStringBuilder builder,
        PropertyModel property,
        string builderVariableName,
        string nodeAccessExpression,
        string displayName,
        bool isProperty)
    {
        if (property.Collection is not null)
        {
            AppendCollection(builder, property, builderVariableName, nodeAccessExpression, displayName, isProperty);
        }
        else if (property.HasValidatableChildren)
        {
            AppendObject(builder, property, builderVariableName, nodeAccessExpression, displayName, isProperty);
        }
        else
        {
            AppendAtom(builder, builderVariableName, nodeAccessExpression, displayName, isProperty);
        }
    }

    private static void AppendAtom(
        IndentedStringBuilder builder,
        string builderVariableName,
        string nodeAccessExpression,
        string displayName,
        bool isProperty)
    {
        builder.AppendLine(
            isProperty
                ? $"{builderVariableName}.AddProperty(\"{displayName}\", [], {nodeAccessExpression});"
                : $"{builderVariableName}.AddItem([], {nodeAccessExpression});");
    }

    private static void AppendObject(
        IndentedStringBuilder builder,
        PropertyModel property,
        string builderVariableName,
        string nodeAccessExpression,
        string displayName,
        bool isProperty)
    {
        string childBuilderName =
            SymbolNameGenerator.MakeCamelCase($"{property.Name}DescriptionBuilder");

        builder.AppendBlock(
            isProperty
                ? $$"""
                  if ({{nodeAccessExpression}} is null)
                  {
                      {{builderVariableName}}.AddProperty("{{displayName}}", [], {{nodeAccessExpression}});
                  }
                  else
                  {
                      {{builderVariableName}}.AddObject("{{displayName}}", [], {{childBuilderName}} =>
                      {
                  """
                : $$"""
                  if ({{nodeAccessExpression}} is null)
                  {
                      {{builderVariableName}}.AddItem([], {{nodeAccessExpression}});
                  }
                  else
                  {
                      {{builderVariableName}}.AddObjectItem([], {{childBuilderName}} =>
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
                isProperty: true);
        }

        builder.AppendBlock(
            """
                    });
                }
            """);
    }

    private static void AppendCollection(
        IndentedStringBuilder builder,
        PropertyModel property,
        string builderVariableName,
        string nodeAccessExpression,
        string displayName,
        bool isProperty)
    {
        CollectionModel collection = property.Collection!;
        string collectionBuilderName =
            SymbolNameGenerator.MakeCamelCase($"{property.Name}CollectionDescriptionBuilder");
        string elementName =
            SymbolNameGenerator.MakeCamelCase($"{property.Name}DescriptionElement");

        bool requiresNullCheck = property.IsNullable || !property.Symbol.Type.IsValueType;
        if (requiresNullCheck)
        {
            builder.AppendBlock(
                isProperty
                    ? $$"""
                      if ({{nodeAccessExpression}} is null)
                      {
                          {{builderVariableName}}.AddProperty("{{displayName}}", [], {{nodeAccessExpression}});
                      }
                      else
                      {
                          {{builderVariableName}}.AddCollection("{{displayName}}", [], {{collectionBuilderName}} =>
                          {
                      """
                    : $$"""
                      if ({{nodeAccessExpression}} is null)
                      {
                          {{builderVariableName}}.AddItem([], {{nodeAccessExpression}});
                      }
                      else
                      {
                          {{builderVariableName}}.AddCollectionItem([], {{collectionBuilderName}} =>
                          {
                      """);
        }
        else
        {
            builder.AppendBlock(
                isProperty
                    ? $$"""
                      {{builderVariableName}}.AddCollection("{{displayName}}", [], {{collectionBuilderName}} =>
                      {
                      """
                    : $$"""
                      {{builderVariableName}}.AddCollectionItem([], {{collectionBuilderName}} =>
                      {
                      """);
        }

        IndentedStringBuilder loopBuilder = builder.IncreaseIndent(requiresNullCheck ? 2 : 1);
        loopBuilder.AppendBlock(
            $$"""
            foreach ({{collection.ElementNullableTypeName}} {{elementName}} in {{nodeAccessExpression}})
            {
            """);

        IndentedStringBuilder elementBody = loopBuilder.IncreaseIndent();
        if (collection.IsElementValidatableType && !collection.ElementChildren.IsDefaultOrEmpty)
        {
            string elementBuilderName =
                SymbolNameGenerator.MakeCamelCase($"{property.Name}ElementDescriptionBuilder");

            elementBody.AppendBlock(
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

            IndentedStringBuilder objectBody = elementBody.IncreaseIndent(2);
            foreach (PropertyModel child in collection.ElementChildren)
            {
                AppendNode(
                    objectBody,
                    child,
                    elementBuilderName,
                    $"{elementName}.{child.Name}",
                    child.Name,
                    isProperty: true);
            }

            elementBody.AppendBlock(
                """
                        });
                    }
                """);
        }
        else
        {
            elementBody.AppendLine($"{collectionBuilderName}.AddItem([], {elementName});");
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
}
