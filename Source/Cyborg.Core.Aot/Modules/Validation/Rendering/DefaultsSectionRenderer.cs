using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Models;

namespace Cyborg.Core.Aot.Modules.Validation.Rendering;

internal sealed class DefaultsSectionRenderer(ValidationContractInfo contractInfo, string rootModuleVariable, DiagnosticsReporter diagnosticsReporter) : ISectionRenderer
{
    private readonly DefaultApplicationRenderer _defaultApplicationRenderer = new(contractInfo, rootModuleVariable, diagnosticsReporter);

    public void RenderSection(IndentedStringBuilder builder, ModuleModel model)
    {
        string qualifiedType = model.FullyQualifiedTypeName;
        builder.AppendBlock(
            $$"""
            async {{KnownTypes.ValueTaskOfT(qualifiedType)}} {{contractInfo.IModuleT.RenderGlobalWithGenerics(qualifiedType)}}.ApplyDefaultsAsync(
                {{contractInfo.IModuleRuntime.RenderGlobal()}} runtime,
                {{KnownTypes.IServiceProvider}} serviceProvider,
                {{KnownTypes.CancellationToken}} cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                {{qualifiedType}} {{rootModuleVariable}} = this;

            """);

        builder = builder.IncreaseIndent();
        _defaultApplicationRenderer.AppendDefaultApplicationForObject(builder, model.Properties, rootModuleVariable, diagnosticsPhase: "defaults");
        builder = builder.DecreaseIndent();
        builder.AppendBlock(
            """
                await global::System.Threading.Tasks.Task.CompletedTask;
                return self;
            }
            """);
    }
}
