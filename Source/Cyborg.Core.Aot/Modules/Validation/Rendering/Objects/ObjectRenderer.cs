using Cyborg.Core.Aot.Modules.Validation.Models;
using Cyborg.Core.Aot.Modules.Validation.Rendering;

namespace Cyborg.Core.Aot.Modules.Validation.Rendering.Objects;

internal readonly record struct ObjectRenderer(ObjectModel Model)
{
    public ObjectShapeRenderer Shape => new(Model.Shape);

    /// <summary>
    /// Emits the common guarded-copy/rewrite/reassign skeleton for a nested validatable object.
    /// </summary>
    public void AppendRewrite(
        IndentedStringBuilder builder,
        string targetVariable,
        string currentVariable,
        Action<IndentedStringBuilder, string> appendRewrite)
    {
        ValueAccess access = Shape.Access(targetVariable);
        if (!access.RequiresGuard)
        {
            builder.AppendLine($"{Model.NonNullableTypeName} {currentVariable} = {access.ValueExpression};");
            appendRewrite(builder, currentVariable);
            builder.AppendLine($"{targetVariable} = {currentVariable};");
            return;
        }

        builder.AppendBlock(
            $$"""
            if ({{access.GuardExpression}})
            {
                {{Model.NonNullableTypeName}} {{currentVariable}} = {{access.ValueExpression}};
            """);
        IndentedStringBuilder guardedBuilder = builder.IncreaseIndent();
        appendRewrite(guardedBuilder, currentVariable);
        guardedBuilder.AppendLine($"{targetVariable} = {currentVariable};");
        builder.AppendLine("}");

        // A non-nullable reference can still be null at runtime. The guard deliberately preserves
        // that invalid state for final validation, so restore the declared flow state afterwards.
        if (Model.Shape.RequiresNullableFlowRelaxation)
        {
            builder.AppendLine($"{ModuleValidationRenderer.Helpers}.{ModuleValidationRenderer.HelperMembers.NullableRelax}({targetVariable});");
        }
    }
}
