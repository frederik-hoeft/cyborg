using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Models;
using System.Text;

namespace Cyborg.Core.Aot.Modules.Validation.Rendering;

internal static class ModuleValidationRenderer
{
    private const string MODULE_VARIABLE = "self";

    public static string Helpers => "__Helpers";

    public static class HelperMembers
    {
        public static string GetDefaultInstance => "__GetDefaultInstance";

        public static string NullableRelax => "__NullableRelax";
    }

    public static string Render(ModuleModel model, ValidationContractInfo contractInfo, DiagnosticsReporter diagnosticsReporter)
    {
        ReadOnlySpan<ISectionRenderer> renderPipeline =
        [
            new DefaultsSectionRenderer(contractInfo, MODULE_VARIABLE, diagnosticsReporter),
            new OverrideSectionRenderer(contractInfo, MODULE_VARIABLE, diagnosticsReporter),
            new ValidationSectionRenderer(contractInfo, diagnosticsReporter),
        ];

        StringBuilder builder = new();
        builder.AppendLine("#nullable enable");
        builder.AppendLine();

        if (!string.IsNullOrWhiteSpace(model.Namespace))
        {
            builder.Append("namespace ").Append(model.Namespace).AppendLine(";");
            builder.AppendLine();
        }

        foreach (ContainingTypeModel containingType in model.ContainingTypes)
        {
            builder.Append(containingType.Declaration).AppendLine();
            builder.AppendLine("{");
        }

        builder.Append("partial record ").Append(model.TypeName).Append(" : ").Append(contractInfo.IModuleT.RenderGlobalWithGenerics(model.TypeName)).AppendLine();
        builder.AppendLine("{");
        IndentedStringBuilder indentedBuilder = new(builder, indentLevel: 1);
        for (int i = 0; i < renderPipeline.Length; ++i)
        {
            if (i > 0)
            {
                builder.AppendLine();
            }
            renderPipeline[i].RenderSection(indentedBuilder, model);
        }
        builder.AppendLine("}");

        for (int index = model.ContainingTypes.Length - 1; index >= 0; --index)
        {
            builder.AppendLine("}");
        }
        builder.AppendLine();
        builder.AppendLine(
            $$"""
            file static class {{Helpers}}
            {
                public static T {{HelperMembers.GetDefaultInstance}}<T>() where T : class, {{contractInfo.IDefaultValueT.RenderGlobalWithGenerics("T")}} => T.Default;

            #pragma warning disable CS8777 // Parameter must have a non-null value when exiting.
                public static void {{HelperMembers.NullableRelax}}<T>([{{KnownTypes.NotNullAttribute}}] T? value) { }
            #pragma warning restore CS8777 // Parameter must have a non-null value when exiting.
            }
            """);

        return builder.ToString();
    }
}
