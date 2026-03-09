using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Models;
using System.Collections.Immutable;

namespace Cyborg.Core.Aot.Modules.Validation;

internal abstract class PropertyValidationAspect
{
    public virtual string? RewriteOverrideResolutionExpression(PropertyModel property, string moduleVariable, string? currentExpression) => currentExpression;

    public virtual string? RewriteDefaultAssignmentExpression(PropertyModel property, string moduleVariable, string? currentExpression) => currentExpression;

    protected virtual void EmitValidation(IndentedStringBuilder builder, ModulePropertyModel property)
    {
    }

    public void EmitValidation(IndentedStringBuilder builder, PropertyModel property, string moduleVariableName)
    {
        ModulePropertyModel model = new(property.Name, property.TypeName, property.Aspects, moduleVariableName);
        EmitValidation(builder, model);
    }

    protected static string CreateValidationError(ModulePropertyModel property, string rule, string message) => 
        $"""
        new global::{typeof(ValidationError).FullName}(nameof({property.AccessExpression}), "{rule}", $"{message}")
        """;

    protected sealed record ModulePropertyModel(string Name, string TypeName, ImmutableArray<PropertyValidationAspect> Aspects, string ModuleVariable)
    {
        public string AccessExpression => field ??= $"{ModuleVariable}.{Name}";
    }
}
