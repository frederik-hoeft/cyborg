using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Models;

namespace Cyborg.Core.Aot.Modules.Validation.Rendering;

internal sealed class DefaultsSectionRenderer
(
    ValidationContractInfo contractInfo,
    VisibilityContext visibilityContext,
    DiagnosticsReporter diagnosticsReporter
) : SectionRenderer(contractInfo, visibilityContext, diagnosticsReporter)
{
    private const string CONTEXT_VARIABLE = "context";

    public override void RenderSection(IndentedStringBuilder builder, ModuleModel model)
    {
        string qualifiedType = model.FullyQualifiedTypeName;
        builder.AppendBlock(
            $$"""
            private async {{KnownTypes.ValueTaskOfT(qualifiedType)}} ApplyDefaultsAsync(
                {{ContractInfo.ModuleValidationContext.RenderGlobal()}} {{CONTEXT_VARIABLE}},
                {{KnownTypes.CancellationToken}} cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                {{qualifiedType}} {{RootModuleVariable}} = this;

            """);

        builder = builder.IncreaseIndent();
        DefaultApplicationRenderer.AppendDefaultApplicationForObject(builder, model.Properties, RootModuleVariable, diagnosticsPhase: "defaults");
        builder = builder.DecreaseIndent();
        builder.AppendBlock(
            $$"""
                await {{KnownTypes.Task}}.CompletedTask;
                return {{RootModuleVariable}};
            }
            """);
    }
}
