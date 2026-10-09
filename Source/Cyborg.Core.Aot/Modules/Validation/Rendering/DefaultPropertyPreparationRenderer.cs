using Cyborg.Core.Aot.Modules.Validation.Aspects;
using Cyborg.Core.Aot.Modules.Validation.Models;

namespace Cyborg.Core.Aot.Modules.Validation.Rendering;

internal sealed class DefaultPropertyPreparationRenderer(SectionRenderer parent) : PropertyPreparationRenderer(parent)
{
    protected override string? CreatePreparedValueExpression(PropertyRewriteContext context)
    {
        string? expression = null;
        foreach (IPropertyDefaultAspect aspect in context.Property.Aspects<IPropertyDefaultAspect>())
        {
            expression = aspect.RewriteDefaultAssignmentExpression(context, expression);
        }
        return expression;
    }
}
