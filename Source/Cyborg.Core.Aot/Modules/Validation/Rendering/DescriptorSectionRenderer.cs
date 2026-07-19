using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Models;

namespace Cyborg.Core.Aot.Modules.Validation.Rendering;

internal sealed class DescriptorSectionRenderer(ValidationContractInfo contractInfo, DiagnosticsReporter diagnosticsReporter) : ISectionRenderer
{
    public void RenderSection(IndentedStringBuilder builder, ModuleModel model)
    {
        builder.AppendBlock(
            $$"""
            public void Describe({{contractInfo.IObjectDescriptionBuilder.RenderGlobal()}} builder)
            {
                {{KnownTypes.ArgumentNullException}}.ThrowIfNull(builder);

            """);

        builder = builder.IncreaseIndent();
        foreach (PropertyModel property in model.Properties)
        {
            AppendDescriptionForNode(builder, property, builderVariableNamePrefix: string.Empty, property.Name, isProperty: true);
        }
        builder = builder.DecreaseIndent();
        builder.AppendBlock(
            $$"""
            }
            """);
    }

    private void AppendDescriptionForNode(IndentedStringBuilder builder, PropertyModel property, string builderVariableNamePrefix, string propertyAccessExpression, bool isProperty)
    {
        List<string> hints = [];
        foreach (PropertyAspect aspect in property.Aspects)
        {
            aspect.RegisterDescriptorHints(hints, diagnosticsReporter, property);
        }
        string hintsExpression = $"[{string.Join(", ", hints.Select(static hint => $"\"{hint}\""))}]";
        if (property.HasValidatableChildren)
        {
            AppendDescriptionForObjectNode(builder, property, builderVariableNamePrefix, propertyAccessExpression, hintsExpression, isProperty);
        }
        else if (property.HasCollectionElementChildren)
        {
            AppendDescriptionForCollectionNode(builder, property, builderVariableNamePrefix, propertyAccessExpression, hintsExpression, isProperty);
        }
        else
        {
            AppendDescriptionForLeafNode(builder, builderVariableNamePrefix, propertyAccessExpression, hintsExpression, isProperty);
        }
    }

    private void AppendDescriptionForLeafNode(IndentedStringBuilder builder, string builderVariableNamePrefix, string nodeAccessExpression, string hintsExpression, bool isProperty)
    {
        string builderVariableName = SymbolNameGenerator.MakeCamelCase($"{builderVariableNamePrefix}Builder");
        if (isProperty)
        {
            builder.AppendBlock(
                $$"""
                {{builderVariableName}}.AddProperty(nameof({{nodeAccessExpression}}), {{hintsExpression}}, {{nodeAccessExpression}});
                """);
        }
        else
        {
            builder.AppendBlock(
                $$"""
                {{builderVariableName}}.AddItem({{hintsExpression}}, {{nodeAccessExpression}});
                """);
        }
    }

    private void AppendDescriptionForObjectNode(IndentedStringBuilder builder, PropertyModel property, string builderVariableNamePrefix, string nodeAccessExpression, string hintsExpression, bool isProperty)
    {
        string childBuilderVariableNamePrefix = $"{builderVariableNamePrefix}{property.Name}";
        string builderVariableName = SymbolNameGenerator.MakeCamelCase($"{builderVariableNamePrefix}Builder");
        string childBuilderVariableName = SymbolNameGenerator.MakeCamelCase($"{childBuilderVariableNamePrefix}Builder");
        if (isProperty)
        {
            builder.AppendBlock(
                $$"""
                if ({{nodeAccessExpression}} is null)
                {
                    {{builderVariableName}}.AddProperty(nameof({{nodeAccessExpression}}), {{hintsExpression}}, {{nodeAccessExpression}});
                }
                else
                {
                    {{builderVariableName}}.AddObject(nameof({{nodeAccessExpression}}), {{hintsExpression}}, {{childBuilderVariableName}} =>
                    {
                """);
        }
        else
        {
            builder.AppendBlock(
                $$"""
                if ({{nodeAccessExpression}} is null)
                {
                    {{builderVariableName}}.AddItem({{hintsExpression}}, {{nodeAccessExpression}});
                }
                else
                {
                    {{builderVariableName}}.AddObjectItem({{hintsExpression}}, {{childBuilderVariableName}} =>
                    {
                """);
        }
        builder = builder.IncreaseIndent(2);
        foreach (PropertyModel child in property.Children)
        {
            AppendDescriptionForNode(builder, child, childBuilderVariableNamePrefix, $"{nodeAccessExpression}.{child.Name}", isProperty: true);
        }
        builder = builder.DecreaseIndent(2);
        builder.AppendBlock(
            $$"""
                });
            }
            """);
    }

    private void AppendDescriptionForCollectionNode(IndentedStringBuilder builder, PropertyModel property, string builderVariableNamePrefix, string nodeAccessExpression, string hintsExpression, bool isProperty)
    {
        CollectionModel collection = property.Collection!;

        string childBuilderVariableNamePrefix = $"{builderVariableNamePrefix}{property.Name}";
        string elementVariable = SymbolNameGenerator.MakeCamelCase($"{childBuilderVariableNamePrefix}Element");
        string builderVariableName = SymbolNameGenerator.MakeCamelCase($"{builderVariableNamePrefix}Builder");
        string childBuilderVariableName = SymbolNameGenerator.MakeCamelCase($"{childBuilderVariableNamePrefix}Builder");

        bool collectionPropertyRequiresNullCheck = property.IsNullable || !property.Symbol.Type.IsValueType;

        _ = (NullCheck: collectionPropertyRequiresNullCheck, IsProperty: isProperty) switch
        {
            (NullCheck: true, IsProperty: true) => builder.AppendBlock(
                $$"""
                if ({{nodeAccessExpression}} is null)
                {
                    {{builderVariableName}}.AddProperty(nameof({{nodeAccessExpression}}), {{hintsExpression}}, {{nodeAccessExpression}});
                }
                else
                {
                    {{builderVariableName}}.AddCollection(nameof({{nodeAccessExpression}}), {{hintsExpression}}, {{childBuilderVariableName}} =>
                    {
                """),
            (NullCheck: true, IsProperty: false) => builder.AppendBlock(
                $$"""
                if ({{nodeAccessExpression}} is null)
                {
                    {{builderVariableName}}.AddItem({{hintsExpression}}, {{nodeAccessExpression}});
                }
                else
                {
                    {{builderVariableName}}.AddCollectionItem({{hintsExpression}}, {{childBuilderVariableName}} =>
                    {
                """),
            (NullCheck: false, IsProperty: true) => builder.AppendBlock(
                $$"""
                {{builderVariableName}}.AddCollection(nameof({{nodeAccessExpression}}), {{hintsExpression}}, {{childBuilderVariableName}} =>
                {
                """),
            (NullCheck: false, IsProperty: false) => builder.AppendBlock(
                $$"""
                {{builderVariableName}}.AddCollectionItem({{hintsExpression}}, {{childBuilderVariableName}} =>
                {
                """)
        };

        int loopIndentLevel = 1;
        if (collectionPropertyRequiresNullCheck)
        {
            loopIndentLevel++;
        }
        IndentedStringBuilder loopBuilder = builder.IncreaseIndent(loopIndentLevel);
        loopBuilder.AppendBlock(
            $$"""
            foreach ({{collection.ElementNullableTypeName}} {{elementVariable}} in {{nodeAccessExpression}})
            {
            """);
        IndentedStringBuilder nestedBuilder = loopBuilder.IncreaseIndent();
        foreach (PropertyModel child in collection.ElementChildren)
        {
            AppendDescriptionForNode(nestedBuilder, child, childBuilderVariableNamePrefix, $"{elementVariable}.{child.Name}", isProperty: false);
        }
        loopBuilder.AppendBlock(
            $$"""
            }
            """);
        if (collectionPropertyRequiresNullCheck)
        {
            builder.AppendBlock(
                $$"""
                    });
                }
                """);
        }
        else
        {
            builder.AppendBlock(
                $$"""
                });
                """);
        }
    }
}
