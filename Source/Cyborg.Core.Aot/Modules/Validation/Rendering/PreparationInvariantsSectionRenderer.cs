using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Models;
using Cyborg.Shared.Text;

namespace Cyborg.Core.Aot.Modules.Validation.Rendering;

internal sealed class PreparationInvariantsSectionRenderer(ValidationContractInfo contractInfo, VisibilityContext visibilityContext, DiagnosticsReporter diagnosticsReporter)
    : PreparationSectionRenderer(contractInfo, visibilityContext, diagnosticsReporter)
{
    protected override string MethodName => ModuleValidationRenderer.ApplyPreparationInvariantsAsync;

    protected override string DiagnosticsPhase => "preparation invariants";

    protected override PropertyPreparationRenderer PreparationRenderer => field ??= new InvariantPropertyPreparationRenderer(this);
}
