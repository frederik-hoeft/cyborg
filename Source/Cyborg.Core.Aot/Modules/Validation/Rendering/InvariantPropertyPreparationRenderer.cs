using Cyborg.Core.Aot.Modules.Validation.Aspects;
using Cyborg.Core.Aot.Modules.Validation.Models;

namespace Cyborg.Core.Aot.Modules.Validation.Rendering;

internal sealed class InvariantPropertyPreparationRenderer(SectionRenderer parent) : PropertyPreparationRenderer(parent)
{
    protected override string? CreatePreparedValueExpression(PropertyRewriteContext context)
    {
        string expression = context.PropertyAccessExpression;
        bool hasRewrite = false;
        foreach (IPropertyPreparationAspect aspect in context.Property.Aspects<IPropertyPreparationAspect>())
        {
            string rewritten = aspect.RewritePreparedValueExpression(context, expression);
            hasRewrite |= !string.Equals(rewritten, expression, StringComparison.Ordinal);
            expression = rewritten;
        }
        return hasRewrite ? expression : null;
    }
}
