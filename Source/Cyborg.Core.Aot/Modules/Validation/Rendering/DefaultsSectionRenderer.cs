using System.Collections.Immutable;
using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Attributess;

namespace Cyborg.Core.Aot.Modules.Validation.Rendering;

internal sealed class DefaultsSectionRenderer(ValidationContractInfo contractInfo, string rootModuleVariable, DiagnosticsReporter diagnosticsReporter) : ISectionRenderer
{
    public void RenderSection(IndentedStringBuilder builder, ModuleModel model)
    {
        string qualifiedType = model.FullyQualifiedTypeName;
        builder.AppendBlock(
            $$"""
            async {{KnownTypes.ValueTaskOfT(qualifiedType)}} {{contractInfo.IModuleT.RenderGlobalWithGenerics(qualifiedType)}}.ApplyDefaultsAsync(
                {{contractInfo.IModuleRuntime.RenderGlobal()}} runtime,
                {{KnownTypes.IServiceProvider}} serviceProvider,
                {{KnownTypes.CancellationToken}} cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                {{qualifiedType}} {{rootModuleVariable}} = this;

            """);

        builder = builder.IncreaseIndent();
        AppendDefaultApplicationForObject(builder, model.Properties, rootModuleVariable);
        builder = builder.DecreaseIndent();
        builder.AppendBlock(
            """
                await global::System.Threading.Tasks.Task.CompletedTask;
                return self;
            }
            """);
    }

    private bool AppendDefaultApplicationForObject(IndentedStringBuilder builder, ImmutableArray<PropertyModel> properties, string targetVariable)
    {
        List<(string PropertyName, string LocalName)> assignments = [];
        foreach (PropertyModel property in properties)
        {
            string propertyAccessExpression = $"{targetVariable}.{property.Name}";
            PropertyRewriteContext rewriteContext = new(property, contractInfo, diagnosticsReporter, rootModuleVariable, propertyAccessExpression);
            string? directExpression = CreateDefaultAssignmentExpression(rewriteContext);
            bool hasDirectAssignment = !string.IsNullOrEmpty(directExpression);
            bool hasChildAssignments = property.IsValidatableType && property.Children.Any(c => HasDefaultWork(c, rewriteContext));
            if (!hasDirectAssignment && !hasChildAssignments)
            {
                continue;
            }
            string localName = $"{targetVariable}_{property.Name}";
            string localInitializer = directExpression ?? propertyAccessExpression;
            builder.AppendLine($"{property.NullableTypeName} {localName} = {localInitializer};");
            if (hasChildAssignments)
            {
                AppendNestedDefaultApplicationForProperty(builder, rewriteContext, localName);
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

    private void AppendNestedDefaultApplicationForProperty(IndentedStringBuilder builder, PropertyRewriteContext rewriteContext, string localName)
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
            AppendDefaultApplicationForObject(builder.IncreaseIndent(), property.Children, nestedVariable);
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
        AppendDefaultApplicationForObject(builder, property.Children, nestedVariable);
        builder.AppendLine($"{localName} = {nestedVariable};");
    }

    private static bool HasDefaultWork(PropertyModel property, PropertyRewriteContext rewriteContext)
    {
        MutablePropertyRewriteContext mutableContext = new(property, rewriteContext.ContractInfo, rewriteContext.DiagnosticsReporter, rewriteContext.ModuleVariable, rewriteContext.PropertyAccessExpression);
        return HasDefaultWork(mutableContext);
    }

    private static bool HasDefaultWork(MutablePropertyRewriteContext rewriteContext)
    {
        string? expression = CreateDefaultAssignmentExpression(rewriteContext);
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
                if (HasDefaultWork(rewriteContext))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static string? CreateDefaultAssignmentExpression(PropertyRewriteContext context)
    {
        string? expression = null;
        foreach (PropertyValidationAspect aspect in context.Property.Aspects)
        {
            expression = aspect.RewriteDefaultAssignmentExpression(context, expression);
        }
        return expression;
    }
}
