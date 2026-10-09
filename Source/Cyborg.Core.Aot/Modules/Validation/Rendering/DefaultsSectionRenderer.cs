using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Models;
using Cyborg.Shared.Text;

namespace Cyborg.Core.Aot.Modules.Validation.Rendering;

internal sealed class DefaultsSectionRenderer(ValidationContractInfo contractInfo, VisibilityContext visibilityContext, DiagnosticsReporter diagnosticsReporter)
    : SectionRenderer(contractInfo, visibilityContext, diagnosticsReporter)
{
    public override void RenderSection(IndentedStringBuilder builder, ModuleModel model)
    {
        AppendPass(builder, model, ModuleValidationRenderer.ApplyDefaultsAsync, applyDefaults: true);
        builder.AppendLine();
        AppendPass(builder, model, ModuleValidationRenderer.ApplyPreparationInvariantsAsync, applyDefaults: false);
    }

    private void AppendPass(IndentedStringBuilder builder, ModuleModel model, string methodName, bool applyDefaults)
    {
        string qualifiedType = model.FullyQualifiedTypeName;
        builder.AppendBlock(
            $$"""
            private async {{KnownTypes.ValueTaskOfT(qualifiedType)}} {{methodName}}(
                {{ContractInfo.ModuleValidationContext.RenderGlobal()}} {{ContextVariable}},
                {{KnownTypes.CancellationToken}} cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                {{qualifiedType}} {{RootModuleVariable}} = this;

            """);

        builder = builder.IncreaseIndent();
        PropertyPreparationRenderer.AppendPreparationForObject(builder, model.Properties, RootModuleVariable,
            diagnosticsPhase: applyDefaults ? "defaults" : "preparation invariants", applyDefaults: applyDefaults);
        builder = builder.DecreaseIndent();
        builder.AppendBlock(
            $$"""
                await {{KnownTypes.Task}}.CompletedTask;
                return {{RootModuleVariable}};
            }
            """);
    }
}
