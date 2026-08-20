using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Models;

namespace Cyborg.Core.Aot.Modules.Validation.Rendering;

internal abstract class SectionRenderer(ValidationContractInfo contractInfo, VisibilityContext visibilityContext, DiagnosticsReporter diagnosticsReporter) : ISectionRenderer
{
    public ValidationContractInfo ContractInfo => contractInfo;

    public VisibilityContext VisibilityContext => visibilityContext;

    public string RootModuleVariable => "self";

    public string ContextVariable => "context";

    public DiagnosticsReporter DiagnosticsReporter => diagnosticsReporter;

    public PropertyPreparationRenderer PropertyPreparationRenderer => field ??= new(parent: this);

    public abstract void RenderSection(IndentedStringBuilder builder, ModuleModel model);
}
