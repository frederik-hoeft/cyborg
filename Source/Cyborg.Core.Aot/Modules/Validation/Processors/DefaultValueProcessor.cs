using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal sealed class DefaultValueProcessor : AttributeProcessorBase
{
    public override string AttributeMetadataName => typeof(DefaultValueAttribute<>).FullName;

    public override bool TryProcess(AttributeData attribute, ref readonly PropertyProcessingContext context, out PropertyAspect? aspect)
    {
        if (!ValidateTypeArguments(attribute, in context, context.Property.Type)
            || !TryGetConstructorArgumentExpression(attribute, argumentIndex: 0, in context, out string? valueExpression))
        {
            return false.WithDefaults(out aspect);
        }

        ImmutableArray<string>.Builder whenPresentExpressions = ImmutableArray.CreateBuilder<string>();
        if (attribute.ConstructorArguments.Length > 1 && !attribute.ConstructorArguments[1].IsNull)
        {
            foreach (TypedConstant item in attribute.ConstructorArguments[1].Values)
            {
                if (!LiteralExpressionFactory.TryGetLiteralExpression(item, context.Property.Type, out string? itemExpression))
                {
                    context.Report(ValidationGeneratorDiagnostics.UnsupportedAttributeLiteral, context.Property.Name, context.ContainingType.Name, GetAttributeFriendlyName(attribute));
                    return false.WithDefaults(out aspect);
                }

                whenPresentExpressions.Add(itemExpression!);
            }
        }

        aspect = new DefaultValueValidationAspect(valueExpression!, whenPresentExpressions.ToImmutable());
        return true;
    }

    private sealed class DefaultValueValidationAspect(string valueExpression, ImmutableArray<string> whenPresentExpressions) : PropertyAspect(ensuresDefault: true)
    {
        public override string? RewriteDefaultAssignmentExpression(PropertyRewriteContext rewriteContext, string? currentExpression)
        {
            string propertyAccessExpression = rewriteContext.PropertyAccessExpression;
            string equalityComparer = KnownTypes.DefaultEqualityComparerOfT(rewriteContext.Property.NullableTypeName);
            string triggerExpression;

            if (whenPresentExpressions.Length == 0)
            {
                triggerExpression = $"{equalityComparer}.Equals({propertyAccessExpression}, default!)";
            }
            else
            {
                List<string> checks = new(whenPresentExpressions.Length);
                foreach (string whenPresentExpression in whenPresentExpressions)
                {
                    checks.Add($"{equalityComparer}.Equals({propertyAccessExpression}, {whenPresentExpression})");
                }

                triggerExpression = string.Join(" || ", checks);
            }

            return $"{triggerExpression} ? {valueExpression} : {propertyAccessExpression}";
        }
    }
}
