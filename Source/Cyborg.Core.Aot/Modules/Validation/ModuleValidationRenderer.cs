using System.Text;
using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Models;

namespace Cyborg.Core.Aot.Modules.Validation;

internal static class ModuleValidationRenderer
{
    private const string MODULE_VARIABLE = "self";

    public static string Render(ModuleModel model, ValidationContractInfo contractInfo)
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

        builder.Append("partial record ").Append(model.TypeName).Append(" : ").Append(contractInfo.IModuleT.RenderGlobalWithGenerics(model.TypeName)).AppendLine();
        builder.AppendLine("{");
        IndentedStringBuilder indentedBuilder = new(builder, indentLevel: 1);
        AppendResolveOverrides(indentedBuilder, model, contractInfo);
        builder.AppendLine();
        AppendApplyDefaults(indentedBuilder, model, contractInfo);
        builder.AppendLine();
        AppendValidate(indentedBuilder, model, contractInfo);
        builder.AppendLine("}");

        for (int index = model.ContainingTypes.Length - 1; index >= 0; --index)
        {
            builder.AppendLine("}");
        }

        return builder.ToString();
    }

    private static void AppendResolveOverrides(IndentedStringBuilder builder, ModuleModel model, ValidationContractInfo contractInfo)
    {
        string qualifiedType = model.FullyQualifiedTypeName;
        builder.AppendBlock(
            $$"""
            public async {{KnownTypes.ValueTaskOfT(qualifiedType)}} ResolveOverridesAsync(
                {{contractInfo.IModuleRuntime.RenderGlobal()}} runtime,
                {{KnownTypes.IServiceProvider}} serviceProvider,
                {{KnownTypes.CancellationToken}} cancellationToken)
            {
                _ = serviceProvider;
                cancellationToken.ThrowIfCancellationRequested();
                {{qualifiedType}} {{MODULE_VARIABLE}} = this;

            """);

        builder = builder.IncreaseIndent();
        AppendOverrideResolutionForObject(builder, model.Properties, MODULE_VARIABLE, MODULE_VARIABLE, MODULE_VARIABLE);
        builder.AppendLine("await global::System.Threading.Tasks.Task.CompletedTask;");
        builder = builder.DecreaseIndent();
        builder.AppendBlock(
            """
                return self;
            }
            """);
    }

    private static void AppendApplyDefaults(IndentedStringBuilder builder, ModuleModel model, ValidationContractInfo contractInfo)
    {
        string qualifiedType = model.FullyQualifiedTypeName;
        builder.AppendBlock(
            $$"""
            public async {{KnownTypes.ValueTaskOfT(qualifiedType)}} ApplyDefaultsAsync(
                {{contractInfo.IModuleRuntime.RenderGlobal()}} runtime,
                {{KnownTypes.IServiceProvider}} serviceProvider,
                {{KnownTypes.CancellationToken}} cancellationToken)
            {
                _ = runtime;
                _ = serviceProvider;
                cancellationToken.ThrowIfCancellationRequested();
                {{qualifiedType}} {{MODULE_VARIABLE}} = this;

            """);

        builder = builder.IncreaseIndent();
        AppendDefaultApplicationForObject(builder, model.Properties, MODULE_VARIABLE, MODULE_VARIABLE);
        builder.AppendLine("await global::System.Threading.Tasks.Task.CompletedTask;");
        builder = builder.DecreaseIndent();
        builder.AppendBlock(
            """
                return self;
            }
            """);
    }

    private static void AppendValidate(IndentedStringBuilder builder, ModuleModel model, ValidationContractInfo contractInfo)
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
                {{qualifiedType}} overridden = await ResolveOverridesAsync(runtime, serviceProvider, cancellationToken);
                {{qualifiedType}} module = await overridden.ApplyDefaultsAsync(runtime, serviceProvider, cancellationToken);
                {{KnownTypes.ListOfT(contractInfo.ValidationError.RenderGlobal())}} errors = [];

            """);

        builder = builder.IncreaseIndent();
        foreach (PropertyModel property in model.Properties)
        {
            AppendValidationForProperty(builder, contractInfo, property, "module", $"module.{property.Name}");
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

    private static bool AppendOverrideResolutionForObject(
        IndentedStringBuilder builder,
        System.Collections.Immutable.ImmutableArray<PropertyModel> properties,
        string targetVariable,
        string rootModuleVariable,
        string rootPathPrefix)
    {
        List<(string PropertyName, string LocalName)> assignments = [];

        foreach (PropertyModel property in properties)
        {
            string propertyAccessExpression = $"{targetVariable}.{property.Name}";
            string rootPathExpression = $"{rootPathPrefix}.{property.Name}";
            string? directExpression = CreateOverrideResolutionExpression(property, rootModuleVariable, propertyAccessExpression, rootPathExpression);
            bool hasDirectAssignment = !string.IsNullOrEmpty(directExpression);
            bool hasChildAssignments = property.IsValidatableType && property.Children.Any(HasOverrideWork);

            if (!hasDirectAssignment && !hasChildAssignments)
            {
                continue;
            }

            string localName = $"{targetVariable}_{property.Name}";
            string localInitializer = directExpression ?? propertyAccessExpression;
            builder.AppendLine($"{property.NullableTypeName} {localName} = {localInitializer};");

            if (hasChildAssignments)
            {
                AppendNestedOverrideResolutionForProperty(builder, property, localName, rootModuleVariable, rootPathExpression);
            }

            assignments.Add((property.Name, localName));
        }

        if (assignments.Count == 0)
        {
            return false;
        }

        builder.AppendLine($"{targetVariable} = {targetVariable} with {{ {string.Join(", ", assignments.Select(static a => $"{a.PropertyName} = {a.LocalName}"))} }};");
        return true;
    }

    private static void AppendNestedOverrideResolutionForProperty(
        IndentedStringBuilder builder,
        PropertyModel property,
        string localName,
        string rootModuleVariable,
        string rootPathExpression)
    {
        string nestedVariable = $"{localName}Current";
        string nestedRootPathPrefix = $"{rootPathExpression}!";

        if (property.IsNullable)
        {
            builder.AppendLine($"if ({localName} is not null)");
            builder.AppendLine("{");
            builder = builder.IncreaseIndent();
            builder.AppendLine($"{property.NonNullableTypeName} {nestedVariable} = {localName};");
            AppendOverrideResolutionForObject(builder, property.Children, nestedVariable, rootModuleVariable, nestedRootPathPrefix);
            builder.AppendLine($"{localName} = {nestedVariable};");
            builder = builder.DecreaseIndent();
            builder.AppendLine("}");
            return;
        }

        builder.AppendLine($"{property.NonNullableTypeName} {nestedVariable} = {localName};");
        AppendOverrideResolutionForObject(builder, property.Children, nestedVariable, rootModuleVariable, nestedRootPathPrefix);
        builder.AppendLine($"{localName} = {nestedVariable};");
    }

    private static bool AppendDefaultApplicationForObject(
        IndentedStringBuilder builder,
        System.Collections.Immutable.ImmutableArray<PropertyModel> properties,
        string targetVariable,
        string rootModuleVariable)
    {
        List<(string PropertyName, string LocalName)> assignments = [];

        foreach (PropertyModel property in properties)
        {
            string propertyAccessExpression = $"{targetVariable}.{property.Name}";
            string? directExpression = CreateDefaultAssignmentExpression(property, rootModuleVariable, propertyAccessExpression);
            bool hasDirectAssignment = !string.IsNullOrEmpty(directExpression);
            bool hasChildAssignments = property.IsValidatableType && property.Children.Any(HasDefaultWork);

            if (!hasDirectAssignment && !hasChildAssignments)
            {
                continue;
            }

            string localName = $"{targetVariable}_{property.Name}";
            string localInitializer = directExpression ?? propertyAccessExpression;
            builder.AppendLine($"{property.NullableTypeName} {localName} = {localInitializer};");

            if (hasChildAssignments)
            {
                AppendNestedDefaultApplicationForProperty(builder, property, localName, rootModuleVariable);
            }

            assignments.Add((property.Name, localName));
        }

        if (assignments.Count == 0)
        {
            return false;
        }

        builder.AppendLine($"{targetVariable} = {targetVariable} with {{ {string.Join(", ", assignments.Select(static a => $"{a.PropertyName} = {a.LocalName}"))} }};");
        return true;
    }

    private static void AppendNestedDefaultApplicationForProperty(
        IndentedStringBuilder builder,
        PropertyModel property,
        string localName,
        string rootModuleVariable)
    {
        string nestedVariable = $"{localName}Current";

        if (property.IsNullable)
        {
            builder.AppendLine($"if ({localName} is not null)");
            builder.AppendLine("{");
            builder = builder.IncreaseIndent();
            builder.AppendLine($"{property.NonNullableTypeName} {nestedVariable} = {localName};");
            AppendDefaultApplicationForObject(builder, property.Children, nestedVariable, rootModuleVariable);
            builder.AppendLine($"{localName} = {nestedVariable};");
            builder = builder.DecreaseIndent();
            builder.AppendLine("}");
            return;
        }

        builder.AppendLine($"{property.NonNullableTypeName} {nestedVariable} = {localName};");
        AppendDefaultApplicationForObject(builder, property.Children, nestedVariable, rootModuleVariable);
        builder.AppendLine($"{localName} = {nestedVariable};");
    }

    private static void AppendValidationForProperty(
        IndentedStringBuilder builder,
        ValidationContractInfo contractInfo,
        PropertyModel property,
        string moduleVariableName,
        string propertyAccessExpression)
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
            AppendValidationForProperty(nestedBuilder, contractInfo, child, moduleVariableName, $"{propertyAccessExpression}.{child.Name}");
        }

        if (nestedRawBuilder.Length == 0)
        {
            return;
        }

        builder.AppendLine($"if ({propertyAccessExpression} is not null)");
        builder.AppendLine("{");
        builder.Raw.Append(nestedRawBuilder.ToString());
        builder.AppendLine("}");
    }

    private static bool HasOverrideWork(PropertyModel property)
    {
        string? expression = CreateOverrideResolutionExpression(property, moduleVariable: "module", propertyAccessExpression: "property", rootPathExpression: "module.Property");
        return !string.IsNullOrEmpty(expression)
            || (property.IsValidatableType && property.Children.Any(HasOverrideWork));
    }

    private static bool HasDefaultWork(PropertyModel property) => property.Aspects.Length > 0
        && !string.IsNullOrEmpty(CreateDefaultAssignmentExpression(property, moduleVariable: "module", propertyAccessExpression: "property"))
        || (property is { IsValidatableType: true, Children.IsDefaultOrEmpty: false } && property.Children.Any(HasDefaultWork));

    private static string? CreateOverrideResolutionExpression(
        PropertyModel property,
        string moduleVariable,
        string propertyAccessExpression,
        string rootPathExpression)
    {
        string? expression = $"runtime.Environment.Resolve({moduleVariable}, {rootPathExpression})";
        foreach (PropertyValidationAspect aspect in property.Aspects)
        {
            expression = aspect.RewriteOverrideResolutionExpression(property, moduleVariable, propertyAccessExpression, expression);
        }

        return expression;
    }

    private static string? CreateDefaultAssignmentExpression(
        PropertyModel property,
        string moduleVariable,
        string propertyAccessExpression)
    {
        string? expression = null;
        foreach (PropertyValidationAspect aspect in property.Aspects)
        {
            expression = aspect.RewriteDefaultAssignmentExpression(property, moduleVariable, propertyAccessExpression, expression);
        }

        return expression;
    }
}