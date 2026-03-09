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

                return new {{KnownTypes.ValueTaskOfT(qualifiedType)}}(this with
                {
            """);

        builder = builder.IncreaseIndent(levels: 2);
        for (int i = 0; i < model.Properties.Length; i++)
        {
            PropertyModel property = model.Properties[i];
            string? expression = $"runtime.Environment.Resolve({MODULE_VARIABLE}, {MODULE_VARIABLE}.{property.Name})";

            foreach (PropertyValidationAspect aspect in property.Aspects)
            {
                expression = aspect.RewriteOverrideResolutionExpression(property, MODULE_VARIABLE, expression);
            }
            if (string.IsNullOrEmpty(expression))
            {
                continue;
            }
            builder.Raw.Append(builder.IndentString).Append(property.Name).Append(" = ").Append(expression).Append(',').AppendLine();
        }
        builder = builder.DecreaseIndent(levels: 2);
        builder.AppendBlock(
            """
                });
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

                return new {{KnownTypes.ValueTaskOfT(qualifiedType)}}(this with
                {
            """);

        builder = builder.IncreaseIndent(levels: 2);
        for (int i = 0; i < model.Properties.Length; i++)
        {
            PropertyModel property = model.Properties[i];
            string? expression = null;

            foreach (PropertyValidationAspect aspect in property.Aspects)
            {
                expression = aspect.RewriteDefaultAssignmentExpression(property, MODULE_VARIABLE, expression);
            }
            if (string.IsNullOrEmpty(expression))
            {
                continue;
            }
            builder.Raw.Append(builder.IndentString).Append(property.Name).Append(" = ").Append(expression).Append(',').AppendLine();
        }
        builder = builder.DecreaseIndent(levels: 2);
        builder.AppendBlock(
            """
                });
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
            foreach (PropertyValidationAspect aspect in property.Aspects)
            {
                aspect.EmitValidation(builder, property, "module");
            }
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
}