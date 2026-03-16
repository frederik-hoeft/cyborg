using System.Collections.Immutable;
using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Models;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Rendering;

internal sealed class OverrideSectionRenderer(ValidationContractInfo contractInfo, string rootModuleVariable, DiagnosticsReporter diagnosticsReporter) : ISectionRenderer
{
    public void RenderSection(IndentedStringBuilder builder, ModuleModel model)
    {
        string qualifiedType = model.FullyQualifiedTypeName;
        builder.AppendBlock(
            $$"""
            async {{KnownTypes.ValueTaskOfT(qualifiedType)}} {{contractInfo.IModuleT.RenderGlobalWithGenerics(qualifiedType)}}.ResolveOverridesAsync(
                {{contractInfo.IModuleRuntime.RenderGlobal()}} runtime,
                {{KnownTypes.IServiceProvider}} serviceProvider,
                {{KnownTypes.CancellationToken}} cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                {{qualifiedType}} {{rootModuleVariable}} = this;

            """);

        builder = builder.IncreaseIndent();
        AppendOverrideResolutionForObject(builder, model.Properties, rootModuleVariable, rootModuleVariable);
        builder = builder.DecreaseIndent();
        builder.AppendBlock(
            """
                await global::System.Threading.Tasks.Task.CompletedTask;
                return self;
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
            PropertyRewriteContext rewriteContext = new(property, contractInfo, diagnosticsReporter, rootModuleVariable, propertyAccessExpression);
            string? directExpression = CreateOverrideResolutionExpression(rewriteContext, rootPathExpression);
            bool hasDirectAssignment = !string.IsNullOrEmpty(directExpression);
            bool hasChildAssignments = property.IsValidatableType && property.Children.Any(c => HasOverrideWork(c, rewriteContext));

            if (!hasDirectAssignment && !hasChildAssignments)
            {
                continue;
            }

            if (property.Symbol.SetMethod is not { } setter || !contractInfo.Compilation.IsSymbolAccessibleWithin(setter, property.Symbol.Type))
            {
                diagnosticsReporter.Report(ValidationGeneratorDiagnostics.PropertyMustBeSettable,
                    property.Symbol.Locations.FirstOrDefault() ?? Location.None,
                    property.Symbol.Name,
                    property.Symbol.ContainingType,
                    "overrides");
                continue;
            }

            string localName = $"{targetVariable}_{property.Name}";
            string localInitializer = directExpression ?? propertyAccessExpression;
            builder.AppendLine($"{property.NullableTypeName} {localName} = {localInitializer};");

            if (hasChildAssignments)
            {
                AppendNestedOverrideResolutionForProperty(builder, rewriteContext, localName, rootPathExpression);
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
        MutablePropertyRewriteContext mutableContext = new(property, rewriteContext.ContractInfo, rewriteContext.DiagnosticsReporter, rewriteContext.ModuleVariable, rewriteContext.PropertyAccessExpression);
        return HasOverrideWork(mutableContext);
    }

    private static bool HasOverrideWork(MutablePropertyRewriteContext rewriteContext)
    {
        string? expression = CreateOverrideResolutionExpression(rewriteContext, rootPathExpression: "module.Property");
        if (!string.IsNullOrEmpty(expression))
        {
            return true;
        }
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

    private static string? CreateOverrideResolutionExpression(PropertyRewriteContext context, string rootPathExpression)
    {
        string? expression = $"runtime.Environment.Resolve({context.ModuleVariable}, {context.PropertyAccessExpression}, valueExpression: \"{rootPathExpression}\")";
        foreach (PropertyValidationAspect aspect in context.Property.Aspects)
        {
            expression = aspect.RewriteOverrideResolutionExpression(context, expression, rootPathExpression);
        }
        return expression;
    }
}
