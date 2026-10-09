using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Models;
using Cyborg.Core.Aot.Modules.Validation.Processors;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Rendering;

internal sealed class ValueExpressionSectionRenderer(ValidationContractInfo contractInfo, VisibilityContext visibilityContext, DiagnosticsReporter diagnosticsReporter)
    : TextualValueSectionRenderer(contractInfo, visibilityContext, diagnosticsReporter)
{
    protected override string MethodName => ModuleValidationRenderer.ResolveValueExpressionsAsync;

    protected override bool ShouldSkip(PropertyModel property) => property.HasAspect<IgnoreValueExpressionAspect>();

    protected override string CreateStringExpression(PropertyModel property, string accessExpression)
    {
        bool willInterpolate = !property.HasAspect<IgnoreInterpolationAspect>();
        return $"{ContextVariable}.ResolveValueExpression({accessExpression}, willInterpolate: {willInterpolate.ToString().ToLowerInvariant()})";
    }

    protected override string CreateElementExpression(ITypeSymbol elementType, string accessExpression) =>
        $"{ContextVariable}.ResolveValueExpression({accessExpression}, willInterpolate: true)";
}
