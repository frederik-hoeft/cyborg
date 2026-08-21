using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Aspects;

internal interface IPropertyDefaultAspect : IPropertyAspect
{
    [return: NotNullIfNotNull(nameof(currentExpression))]
    string? RewriteDefaultAssignmentExpression(PropertyRewriteContext context, string? currentExpression);
}
