namespace Cyborg.Core.Aot.Modules.Validation.Aspects;

internal interface IPropertyPreparationAspect : IPropertyAspect
{
    /// <summary>
    /// Rewrites the effective property value to enforce preparation invariants, such as applying destination
    /// tags after defaults, overrides, and typed value expressions have been resolved.
    /// The resulting value proceeds to textual interpolation and constraint validation.
    /// </summary>
    string RewritePreparedValueExpression(PropertyRewriteContext context, string currentExpression);
}
