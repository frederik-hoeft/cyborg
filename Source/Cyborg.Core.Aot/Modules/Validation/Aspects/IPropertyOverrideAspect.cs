namespace Cyborg.Core.Aot.Modules.Validation.Aspects;

internal interface IPropertyOverrideAspect : IPropertyAspect
{
    string RewriteOverrideResolutionExpression(PropertyRewriteContext context, string currentExpression, string rootPathExpression);
}
