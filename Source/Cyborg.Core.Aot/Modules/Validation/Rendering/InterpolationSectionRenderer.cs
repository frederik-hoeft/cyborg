using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Models;
using Cyborg.Core.Aot.Modules.Validation.Processors;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Rendering;

internal sealed class InterpolationSectionRenderer(ValidationContractInfo contractInfo, VisibilityContext visibilityContext, DiagnosticsReporter diagnosticsReporter)
    : TextualValueSectionRenderer(contractInfo, visibilityContext, diagnosticsReporter)
{
    protected override string MethodName => ModuleValidationRenderer.ApplyInterpolationAsync;

    protected override bool ShouldSkip(PropertyModel property) => property.HasAspect<IgnoreInterpolationAspect>();

    protected override string CreateStringExpression(PropertyModel property, string accessExpression)
    {
        string interpolated = $"{ContextVariable}.Interpolate({accessExpression})";
        return property.Symbol.Type.EqualsIgnoreNullability(ContractInfo.TaggedString) ? interpolated : $"{interpolated}.Value";
    }

    protected override string CreateElementExpression(ITypeSymbol elementType, string accessExpression)
    {
        string interpolated = $"{ContextVariable}.Interpolate({accessExpression})";
        return elementType.EqualsIgnoreNullability(ContractInfo.TaggedString) ? interpolated : $"{interpolated}.Value";
    }
}
