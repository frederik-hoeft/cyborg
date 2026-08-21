using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Aspects;
using Cyborg.Core.Aot.Modules.Validation.Models;
using Cyborg.Core.Aot.Modules.Validation.Processors;
using Cyborg.Core.Aot.Modules.Validation.Rendering.Models;
using Cyborg.Core.Aot.Modules.Validation.Rendering.Objects;
using Cyborg.Shared.Text;
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
            bool hasChildAssignments = property.Object is { HasChildren: true } objectModel
                && objectModel.Children.Any(child => HasOverrideWork(child, rewriteContext));
            bool hasCollectionElementAssignments = property.Collection is { Shape.SupportsElementRewrite: true } collection
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
        ObjectModel objectModel = rewriteContext.Property.Object
            ?? throw new InvalidOperationException($"Nested override resolution requires object metadata for property '{rewriteContext.Property.Name}'.");
        string nestedVariable = $"{localName}Current";

        objectModel.Renderer.AppendRewrite(
            builder,
            localName,
            nestedVariable,
            (nestedBuilder, currentVariable) => AppendOverrideResolutionForObject(nestedBuilder, objectModel.Children, currentVariable, rootPathExpression));
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
        if (property.Object is { } objectModel)
        {
            foreach (PropertyModel child in objectModel.Children)
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
        string expression = context.Property.Symbol.Type.EqualsIgnoreNullability(SpecialType.System_String)
            ? $"{ContextVariable}.SelectRawStringOverride({arguments})"
            : context.Property.Symbol.Type.EqualsIgnoreNullability(ContractInfo.TaggedString)
                ? $"{ContextVariable}.SelectRawTaggedStringOverride({arguments})"
                : $"{ContextVariable}.ResolveOverride({arguments})";
        foreach (IPropertyOverrideAspect aspect in context.Property.Aspects<IPropertyOverrideAspect>())
        {
            expression = aspect.RewriteOverrideResolutionExpression(context, expression, rootPathExpression);
        }
        return expression;
    }
}
