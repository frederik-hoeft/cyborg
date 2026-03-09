using System.Text;
using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Models;
using Microsoft.CodeAnalysis.CSharp;

namespace Cyborg.Core.Aot.Modules.Validation;

internal static class ModuleValidationRenderer
{
    private const string MODULE_VARIABLE = "self";

    public static string Render(ModuleModel model)
    {
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

        builder.Append("partial record ").Append(model.TypeName).Append(" : ").Append(KnownTypes.IModuleOfT(model.TypeName)).AppendLine();
        builder.AppendLine("{");
        IndentedStringBuilder indentedBuilder = new(builder, indentLevel: 1);
        AppendResolveOverrides(indentedBuilder, model);
        builder.AppendLine();
        AppendApplyDefaults(indentedBuilder, model);
        builder.AppendLine();
        AppendValidate(indentedBuilder, model);
        builder.AppendLine("}");

        for (int index = model.ContainingTypes.Length - 1; index >= 0; index--)
        {
            builder.AppendLine("}");
        }

        return builder.ToString();
    }

    private static void AppendResolveOverrides(IndentedStringBuilder builder, ModuleModel model)
    {
        string qualifiedType = model.FullyQualifiedTypeName;
        builder.AppendBlock(
            $$"""
            public {{KnownTypes.ValueTaskOfT(qualifiedType)}} ResolveOverridesAsync(
                {{KnownTypes.IModuleRuntime}} runtime,
                {{KnownTypes.IServiceProvider}} serviceProvider,
                {{KnownTypes.CancellationToken}} cancellationToken)
            {
                _ = serviceProvider;
                cancellationToken.ThrowIfCancellationRequested();
                {{qualifiedType}} {{MODULE_VARIABLE}} = this;

            """);

        builder = builder.IncreaseIndent();
        foreach (PropertyModel property in model.Properties)
        {
            AppendOverrideResolutionForProperty(builder, property, MODULE_VARIABLE, MODULE_VARIABLE, $"{MODULE_VARIABLE}.{property.Name}", $"{MODULE_VARIABLE}.{property.Name}", property.Name);
        }
        builder = builder.DecreaseIndent();
        builder.AppendBlock(
            $$"""
                return new {{KnownTypes.ValueTaskOfT(qualifiedType)}}({{MODULE_VARIABLE}});
            }
            """);
    }

    private static void AppendApplyDefaults(IndentedStringBuilder builder, ModuleModel model)
    {
        string qualifiedType = model.FullyQualifiedTypeName;
        builder.AppendBlock(
            $$"""
            public {{KnownTypes.ValueTaskOfT(qualifiedType)}} ApplyDefaultsAsync(
                {{KnownTypes.IModuleRuntime}} runtime,
                {{KnownTypes.IServiceProvider}} serviceProvider,
                {{KnownTypes.CancellationToken}} cancellationToken)
            {
                _ = runtime;
                _ = serviceProvider;
                cancellationToken.ThrowIfCancellationRequested();
                {{qualifiedType}} {{MODULE_VARIABLE}} = this;

            """);

        builder = builder.IncreaseIndent();
        foreach (PropertyModel property in model.Properties)
        {
            AppendDefaultApplicationForProperty(builder, property, MODULE_VARIABLE, $"{MODULE_VARIABLE}.{property.Name}", property.Name);
        }
        builder = builder.DecreaseIndent();
        builder.AppendBlock(
            $$"""
                return new {{KnownTypes.ValueTaskOfT(qualifiedType)}}({{MODULE_VARIABLE}});
            }
            """);
    }

    private static void AppendValidate(IndentedStringBuilder builder, ModuleModel model)
    {
        string qualifiedType = model.FullyQualifiedTypeName;

        builder.AppendBlock(
            $$"""
            public async {{KnownTypes.ValueTaskOfT(KnownTypes.ValidationResultOfT(qualifiedType))}} ValidateAsync(
                {{KnownTypes.IModuleRuntime}} runtime,
                {{KnownTypes.IServiceProvider}} serviceProvider,
                {{KnownTypes.CancellationToken}} cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                {{qualifiedType}} overridden = await ResolveOverridesAsync(runtime, serviceProvider, cancellationToken);
                {{qualifiedType}} module = await overridden.ApplyDefaultsAsync(runtime, serviceProvider, cancellationToken);
                {{KnownTypes.ListOfT(KnownTypes.ValidationError)}} errors = [];

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
                    ? {{KnownTypes.ValidationResultOfT(qualifiedType)}}.Valid(module)
                    : {{KnownTypes.ValidationResultOfT(qualifiedType)}}.Invalid(errors);
            }
            """);
    }

    public static string Quote(string value) => SymbolDisplay.FormatLiteral(value, quote: true);

    private static void AppendOverrideResolutionForProperty(
        IndentedStringBuilder builder,
        PropertyModel property,
        string targetVariable,
        string rootModuleVariable,
        string propertyAccessExpression,
        string rootPathExpression,
        string assignmentName)
    {
        string? expression = $"runtime.Environment.Resolve({rootModuleVariable}, {rootPathExpression})";
        foreach (PropertyValidationAspect aspect in property.Aspects)
        {
            expression = aspect.RewriteOverrideResolutionExpression(property, rootModuleVariable, propertyAccessExpression, expression);
        }

        if (!string.IsNullOrEmpty(expression))
        {
            builder.AppendLine($"{targetVariable} = {targetVariable} with {{ {assignmentName} = {expression} }};");
        }

        if (!property.IsValidatableType || property.Children.IsDefaultOrEmpty)
        {
            return;
        }

        builder.AppendLine($"if ({propertyAccessExpression} is not null)");
        builder.AppendLine("{");
        builder = builder.IncreaseIndent();
        string nestedVariable = $"{assignmentName}Nested";
        builder.AppendLine($"var {nestedVariable} = {propertyAccessExpression};");
        foreach (PropertyModel child in property.Children)
        {
            string childAccess = $"{nestedVariable}.{child.Name}";
            string childRootPath = $"{rootPathExpression}! .{child.Name}".Replace("! .", "!.");
            AppendOverrideResolutionForProperty(builder, child, nestedVariable, rootModuleVariable, childAccess, childRootPath, child.Name);
        }
        builder.AppendLine($"{targetVariable} = {targetVariable} with {{ {assignmentName} = {nestedVariable} }};");
        builder = builder.DecreaseIndent();
        builder.AppendLine("}");
    }

    private static void AppendDefaultApplicationForProperty(IndentedStringBuilder builder, PropertyModel property, string rootModuleVariable, string propertyAccessExpression, string assignmentName)
    {
        string? expression = null;
        foreach (PropertyValidationAspect aspect in property.Aspects)
        {
            expression = aspect.RewriteDefaultAssignmentExpression(property, rootModuleVariable, propertyAccessExpression, expression);
        }

        if (!string.IsNullOrEmpty(expression))
        {
            builder.AppendLine($"{rootModuleVariable} = {rootModuleVariable} with {{ {assignmentName} = {expression} }};");
        }

        if (!property.IsValidatableType || property.Children.IsDefaultOrEmpty)
        {
            return;
        }

        builder.AppendLine($"if ({propertyAccessExpression} is not null)");
        builder.AppendLine("{");
        builder = builder.IncreaseIndent();
        string nestedVariable = $"{assignmentName}Nested";
        builder.AppendLine($"var {nestedVariable} = {propertyAccessExpression};");
        foreach (PropertyModel child in property.Children)
        {
            AppendDefaultApplicationForProperty(builder, child, nestedVariable, $"{nestedVariable}.{child.Name}", child.Name);
        }
        builder.AppendLine($"{rootModuleVariable} = {rootModuleVariable} with {{ {assignmentName} = {nestedVariable} }};");
        builder = builder.DecreaseIndent();
        builder.AppendLine("}");
    }

    private static void AppendValidationForProperty(IndentedStringBuilder builder, PropertyModel property, string moduleVariableName, string propertyAccessExpression)
    {
        foreach (PropertyValidationAspect aspect in property.Aspects)
        {
            aspect.EmitValidation(builder, property, moduleVariableName, propertyAccessExpression);
        }

        if (!property.IsValidatableType || property.Children.IsDefaultOrEmpty)
        {
            return;
        }

        builder.AppendLine($"if ({propertyAccessExpression} is not null)");
        builder.AppendLine("{");
        builder = builder.IncreaseIndent();
        foreach (PropertyModel child in property.Children)
        {
            AppendValidationForProperty(builder, child, moduleVariableName, $"{propertyAccessExpression}.{child.Name}");
        }
        builder = builder.DecreaseIndent();
        builder.AppendLine("}");
    }
}
