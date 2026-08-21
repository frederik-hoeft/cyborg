using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Models;
using Cyborg.Core.Aot.Modules.Validation.Processors;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Cyborg.Core.Aot.Modules.Validation.Rendering;

internal sealed class OverrideSectionRenderer(ValidationContractInfo contractInfo, VisibilityContext visibilityContext, DiagnosticsReporter diagnosticsReporter)
    : SectionRenderer(contractInfo, visibilityContext, diagnosticsReporter)
{
    public override void RenderSection(IndentedStringBuilder builder, ModuleModel model)
    {
        string qualifiedType = model.FullyQualifiedTypeName;
        builder.AppendBlock(
            $$"""
            private async {{KnownTypes.ValueTaskOfT(qualifiedType)}} {{ModuleValidationRenderer.ResolveOverridesAsync}}(
                {{ContractInfo.ModuleValidationContext.RenderGlobal()}} {{ContextVariable}},
                {{KnownTypes.CancellationToken}} cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                {{qualifiedType}} {{RootModuleVariable}} = this;

            """);

        builder = builder.IncreaseIndent();
        AppendOverrideResolutionForObject(builder, model.Properties, RootModuleVariable, RootModuleVariable);
        builder = builder.DecreaseIndent();
        builder.AppendBlock(
            $$"""
                await {{KnownTypes.Task}}.CompletedTask;
                return {{RootModuleVariable}};
            }
            """);
    }

    private bool AppendOverrideResolutionForObject(IndentedStringBuilder builder, ImmutableArray<PropertyModel> properties, string targetVariable, string rootPathPrefix)
    {
        List<(string PropertyName, string LocalName)> assignments = [];

        foreach (PropertyModel property in properties)
        {
            string propertyAccessExpression = $"{targetVariable}.{property.Name}";
            string rootPathExpression = $"{rootPathPrefix}.{property.Name}";
            PropertyRewriteContext rewriteContext = new(property, ContractInfo, DiagnosticsReporter, RootModuleVariable, ContextVariable, propertyAccessExpression);
            bool hasChildAssignments = property.IsValidatableType && property.Children.Any(child => HasOverrideWork(child, rewriteContext));
            bool hasCollectionElementAssignments = property.Collection is { SupportsElementRewrite: true } collection
                && property.HasCollectionElementChildren
                && PropertyPreparationRenderer.HasCollectionPreparationWork(collection, rewriteContext);
            bool ignoreOverride = property.TryGetAspect(out IgnoreOverrideAspect? ignoreOverrideAspect);
            if (ignoreOverride && (ignoreOverrideAspect is { Recurse: true } || !hasChildAssignments && !hasCollectionElementAssignments))
            {
                continue;
            }

            if (property.Symbol.SetMethod is not { } setter || !VisibilityContext.IsVisible(setter))
            {
                DiagnosticsReporter.Report(ValidationGeneratorDiagnostics.PropertyMustBeSettable,
                    property.Symbol.Locations.FirstOrDefault() ?? Location.None,
                    property.Symbol.Name,
                    property.Symbol.ContainingType,
                    "overrides");
                continue;
            }

            string localName = $"{targetVariable}_{property.Name}";
            string localInitializer = ignoreOverride ? propertyAccessExpression : CreateOverrideResolutionExpression(rewriteContext, rootPathExpression);
            builder.AppendLine($"{property.NullableTypeName} {localName} = {localInitializer};");
            PropertyRewriteContext nestedRewriteContext = rewriteContext with
            {
                PropertyAccessExpression = localName
            };
            PropertyPreparationRenderer.AppendDirectPreparationForProperty(builder, nestedRewriteContext);

            if (hasChildAssignments)
            {
                AppendNestedOverrideResolutionForProperty(builder, rewriteContext, localName, rootPathExpression);
            }

            if (hasCollectionElementAssignments)
            {
                PropertyPreparationRenderer.AppendCollectionPreparationForProperty(builder, property, localName, diagnosticsPhase: "overrides");
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

    private void AppendNestedOverrideResolutionForProperty(IndentedStringBuilder builder, PropertyRewriteContext rewriteContext, string localName, string rootPathExpression)
    {
        string nestedVariable = $"{localName}Current";

        PropertyModel property = rewriteContext.Property;
        if (property.IsNullable || !property.HasDefault)
        {
            builder.AppendBlock(
                $$"""
                if ({{localName}} is not null)
                {
                    {{property.NonNullableTypeName}} {{nestedVariable}} = {{localName}};
                """);
            AppendOverrideResolutionForObject(builder.IncreaseIndent(), property.Children, nestedVariable, rootPathExpression);
            builder.AppendBlock(
                $$"""
                    {{localName}} = {{nestedVariable}};
                }
                """);
            if (!property.IsNullable)
            {
                // relax nullability since we added the null check even if there weren't any annotations
                builder.AppendLine($"{ModuleValidationRenderer.Helpers}.{ModuleValidationRenderer.HelperMembers.NullableRelax}({localName});");
            }
            return;
        }

        builder.AppendLine($"{property.NonNullableTypeName} {nestedVariable} = {localName};");
        AppendOverrideResolutionForObject(builder, property.Children, nestedVariable, rootPathExpression);
        builder.AppendLine($"{localName} = {nestedVariable};");
    }

    private static bool HasOverrideWork(PropertyModel property, PropertyRewriteContext rewriteContext)
    {
        MutablePropertyRewriteContext mutableContext = new(property, rewriteContext.ContractInfo, rewriteContext.DiagnosticsReporter, rewriteContext.ModuleVariable,
            rewriteContext.ContextVariable, rewriteContext.PropertyAccessExpression);
        return HasOverrideWork(mutableContext);
    }

    private static bool HasOverrideWork(MutablePropertyRewriteContext rewriteContext)
    {
        if (!rewriteContext.Property.TryGetAspect(out IgnoreOverrideAspect? ignoreOverrideAspect))
        {
            return true;
        }
        if (ignoreOverrideAspect.Recurse)
        {
            return false;
        }
        // this property is marked to ignore overrides (don't resolve this exact node), but child properties may still be valid targets
        PropertyModel property = rewriteContext.Property;
        if (property.IsValidatableType)
        {
            foreach (PropertyModel child in property.Children)
            {
                rewriteContext.SetProperty(child);
                if (HasOverrideWork(rewriteContext))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private string CreateOverrideResolutionExpression(PropertyRewriteContext context, string rootPathExpression)
    {
        string arguments = $"{context.ModuleVariable}, {context.PropertyAccessExpression}, moduleExpression: \"{context.ModuleVariable}\", valueExpression: \"{rootPathExpression}\"";
        string expression = context.Property.Symbol.Type.IsStringType()
            ? $"{ContextVariable}.SelectRawStringOverride({arguments})"
            : context.Property.Symbol.Type.IsOrNullableOf(ContractInfo.TaggedString)
                ? $"{ContextVariable}.SelectRawTaggedStringOverride({arguments})"
                : $"{ContextVariable}.ResolveOverride({arguments})";
        foreach (PropertyAspect aspect in context.Property.Aspects)
        {
            expression = aspect.RewriteOverrideResolutionExpression(context, expression, rootPathExpression);
        }
        return expression;
    }
}
