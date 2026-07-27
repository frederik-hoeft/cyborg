using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Models;

namespace Cyborg.Core.Aot.Modules.Validation.Rendering;

/// <summary>
/// Emits <c>ToString</c> (short module identity) and <c>Inspect</c> (full recursive state dump)
/// for modules annotated with <c>[GeneratedModuleValidation]</c>.
/// </summary>
internal sealed class InspectionSectionRenderer : ISectionRenderer
{
    // TODO: fix this hacky global type name references to use proper, dynamically discovered type contracts instead
    private const string MODULE_IDENTITY_TYPE = "global::Cyborg.Core.Modules.Debugging.ModuleIdentity";
    private const string MODULE_INSPECTION_TYPE = "global::Cyborg.Core.Modules.Debugging.ModuleInspection";
    private const string STRING_BUILDER_TYPE = "global::System.Text.StringBuilder";

    public void RenderSection(IndentedStringBuilder builder, ModuleModel model)
    {
        builder.AppendBlock(
            $$"""
            public override string ToString() => {{MODULE_IDENTITY_TYPE}}.Format(ModuleId, Name, Group);

            public string Inspect()
            {
                {{STRING_BUILDER_TYPE}} builder = new();
                builder.AppendLine(ToString());
            """);

        IndentedStringBuilder body = builder.IncreaseIndent();
        // TODO: this is awful, this should be a truly recursive inspection of the module and its properties
        foreach (PropertyModel property in model.Properties)
        {
            // Skip identity fields already rendered by ToString to avoid noise, but still inspect Artifacts etc.
            if (property.Name is "Name" or "Group")
            {
                continue;
            }

            body.AppendLine($"{MODULE_INSPECTION_TYPE}.AppendProperty(builder, nameof({property.Name}), {property.Name}, indentLevel: 1);");
        }

        builder.AppendBlock(
            """
                return builder.ToString();
            }
            """);
    }
}
