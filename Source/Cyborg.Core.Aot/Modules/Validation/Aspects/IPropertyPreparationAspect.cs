namespace Cyborg.Core.Aot.Modules.Validation.Aspects;

internal interface IPropertyPreparationAspect : IPropertyAspect
{
    /// <summary>
    /// Re-applies property-level invariants after ordinary default resolution. This stage runs both
    /// before and after override resolution, so destination invariants cannot be removed by an override.
    /// </summary>
    string RewritePreparedValueExpression(PropertyRewriteContext context, string currentExpression);
}
