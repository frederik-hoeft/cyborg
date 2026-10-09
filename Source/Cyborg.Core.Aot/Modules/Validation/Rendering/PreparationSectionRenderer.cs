using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Models;
using Cyborg.Shared.Text;

namespace Cyborg.Core.Aot.Modules.Validation.Rendering;

internal abstract class PreparationSectionRenderer(ValidationContractInfo contractInfo, VisibilityContext visibilityContext, DiagnosticsReporter diagnosticsReporter)
    : SectionRenderer(contractInfo, visibilityContext, diagnosticsReporter)
{
    protected abstract string MethodName { get; }

    protected abstract string DiagnosticsPhase { get; }

    protected abstract PropertyPreparationRenderer PreparationRenderer { get; }

    public override void RenderSection(IndentedStringBuilder builder, ModuleModel model)
    {
        string qualifiedType = model.FullyQualifiedTypeName;
        builder.AppendBlock(
            $$"""
            private async {{KnownTypes.ValueTaskOfT(qualifiedType)}} {{MethodName}}(
                {{ContractInfo.ModuleValidationContext.RenderGlobal()}} {{ContextVariable}},
                {{KnownTypes.CancellationToken}} cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                {{qualifiedType}} {{RootModuleVariable}} = this;

            """);

        builder = builder.IncreaseIndent();
        PreparationRenderer.AppendPreparationForObject(builder, model.Properties, RootModuleVariable, DiagnosticsPhase);
        builder = builder.DecreaseIndent();
        builder.AppendBlock(
            $$"""
                await {{KnownTypes.Task}}.CompletedTask;
                return {{RootModuleVariable}};
            }
            """);
    }
}
