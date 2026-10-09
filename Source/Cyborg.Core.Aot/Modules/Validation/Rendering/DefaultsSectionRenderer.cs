using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Models;
using Cyborg.Shared.Text;

namespace Cyborg.Core.Aot.Modules.Validation.Rendering;

internal sealed class DefaultsSectionRenderer(ValidationContractInfo contractInfo, VisibilityContext visibilityContext, DiagnosticsReporter diagnosticsReporter)
    : PreparationSectionRenderer(contractInfo, visibilityContext, diagnosticsReporter)
{
    protected override string MethodName => ModuleValidationRenderer.ApplyDefaultsAsync;

    protected override string DiagnosticsPhase => "defaults";

    protected override PropertyPreparationRenderer PreparationRenderer => field ??= new DefaultPropertyPreparationRenderer(this);
}
