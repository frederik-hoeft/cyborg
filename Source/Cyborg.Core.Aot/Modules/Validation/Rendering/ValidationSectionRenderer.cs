using System.Text;
using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Models;

namespace Cyborg.Core.Aot.Modules.Validation.Rendering;

internal sealed class ValidationSectionRenderer(ValidationContractInfo contractInfo) : ISectionRenderer
{
    public void RenderSection(IndentedStringBuilder builder, ModuleModel model)
    {
        string qualifiedType = model.FullyQualifiedTypeName;

        builder.AppendBlock(
            $$"""
            public async {{KnownTypes.ValueTaskOfT(contractInfo.ValidationResultT.RenderGlobalWithGenerics(qualifiedType))}} ValidateAsync(
                {{contractInfo.IModuleRuntime.RenderGlobal()}} runtime,
                {{KnownTypes.IServiceProvider}} serviceProvider,
                {{KnownTypes.CancellationToken}} cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                {{contractInfo.IModuleT.RenderGlobalWithGenerics(qualifiedType)}} self = this;
                {{contractInfo.IModuleT.RenderGlobalWithGenerics(qualifiedType)}} withDefaults = await self.ApplyDefaultsAsync(runtime, serviceProvider, cancellationToken);
                {{qualifiedType}} module = await withDefaults.ResolveOverridesAsync(runtime, serviceProvider, cancellationToken);
                {{KnownTypes.ListOfT(contractInfo.ValidationError.RenderGlobal())}} errors = [];

            """);

        builder = builder.IncreaseIndent();
        foreach (PropertyModel property in model.Properties)
        {
            AppendValidationForProperty(builder, property, "module", $"module.{property.Name}");
        }
        builder = builder.DecreaseIndent();
        builder.AppendBlock(
            $$"""
                return errors.Count == 0
                    ? {{contractInfo.ValidationResultT.RenderGlobalWithGenerics(qualifiedType)}}.Valid(module)
                    : {{contractInfo.ValidationResultT.RenderGlobalWithGenerics(qualifiedType)}}.Invalid(errors);
            }
            """);
    }

    private void AppendValidationForProperty(IndentedStringBuilder builder, PropertyModel property, string moduleVariableName, string propertyAccessExpression)
    {
        foreach (PropertyValidationAspect aspect in property.Aspects)
        {
            aspect.EmitValidation(builder, contractInfo, property, moduleVariableName, propertyAccessExpression);
        }

        if (!property.IsValidatableType || property.Children.IsDefaultOrEmpty)
        {
            return;
        }

        StringBuilder nestedRawBuilder = new();
        IndentedStringBuilder nestedBuilder = new(nestedRawBuilder, indentLevel: builder.IndentLevel + 1);

        foreach (PropertyModel child in property.Children)
        {
            AppendValidationForProperty(nestedBuilder, child, moduleVariableName, $"{propertyAccessExpression}.{child.Name}");
        }

        if (nestedRawBuilder.Length == 0)
        {
            return;
        }
        builder.AppendBlock(
            $$"""
            if ({{propertyAccessExpression}} is not null)
            {
            """);
        builder.Raw.Append(nestedRawBuilder.ToString());
        builder.AppendLine("}");
    }
}
