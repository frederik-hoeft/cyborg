using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Aspects;
using Cyborg.Core.Aot.Modules.Validation.Models;
using Cyborg.Core.Aot.Modules.Validation.Rendering.Collections;
using Cyborg.Core.Aot.Modules.Validation.Rendering.Objects;
using Cyborg.Shared.Text;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Rendering;

internal sealed class ValidationSectionRenderer(ValidationContractInfo contractInfo, VisibilityContext visibilityContext, DiagnosticsReporter diagnosticsReporter)
    : SectionRenderer(contractInfo, visibilityContext, diagnosticsReporter)
{
    public ValidationVariables Variables => field ??= new ValidationVariables(RootModuleVariable, ContextVariable, Errors: "errors");

    public override void RenderSection(IndentedStringBuilder builder, ModuleModel model)
    {
        string qualifiedType = model.FullyQualifiedTypeName;
        string validationContextType = ContractInfo.ModuleValidationContext.RenderGlobal();

        builder.AppendBlock(
            $$"""
            public async {{KnownTypes.ValueTaskOfT(ContractInfo.IValidationResultT.RenderGlobalWithGenerics(qualifiedType))}} {{ModuleValidationRenderer.ValidateAsync}}(
                {{ContractInfo.IModuleRuntime.RenderGlobal()}} runtime,
                {{KnownTypes.IServiceProvider}} serviceProvider,
                {{KnownTypes.CancellationToken}} cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                {{validationContextType}} {{Variables.Context}} = {{validationContextType}}.Create(runtime, serviceProvider);
                // apply defaults first to avoid overriding properties of null objects
                {{qualifiedType}} {{Variables.Module}} = await this.{{ModuleValidationRenderer.ApplyDefaultsAsync}}({{Variables.Context}}, cancellationToken);
                // resolve any overrides that may have been applied to the module
                {{Variables.Module}} = await {{Variables.Module}}.{{ModuleValidationRenderer.ResolveOverridesAsync}}({{Variables.Context}}, cancellationToken);
                // ensure that defaults are also applied to values injected via overrides
                {{Variables.Module}} = await {{Variables.Module}}.{{ModuleValidationRenderer.ApplyDefaultsAsync}}({{Variables.Context}}, cancellationToken);
                // interpolate all string members against the runtime environment
                {{Variables.Module}} = await {{Variables.Module}}.{{ModuleValidationRenderer.ApplyInterpolationAsync}}({{Variables.Context}}, cancellationToken);
                {{KnownTypes.ListOfT(ContractInfo.ValidationError.RenderGlobal())}} {{Variables.Errors}} = [];

            """);

        builder = builder.IncreaseIndent();
        foreach (PropertyModel property in model.Properties)
        {
            AppendValidationForProperty(
                builder,
                property,
                $"{Variables.Module}.{property.Name}",
                ValidationPath.ForProperty(property.Name));
        }
        builder = builder.DecreaseIndent();
        builder.AppendBlock(
            $$"""
                return {{Variables.Errors}}.Count == 0
                    ? {{ContractInfo.ValidationResult.RenderGlobal()}}.Valid({{Variables.Module}})
                    : {{ContractInfo.ValidationResult.RenderGlobal()}}.Invalid({{Variables.Module}}, {{Variables.Errors}});
            }
            """);
    }

    private void AppendValidationForProperty(
        IndentedStringBuilder builder,
        PropertyModel property,
        string propertyAccessExpression,
        ValidationPath path)
    {
        foreach (IPropertyValidationAspect aspect in property.Aspects<IPropertyValidationAspect>())
        {
            if (aspect is not CollectionElementValidationAspect)
            {
                aspect.EmitValidation(this, builder, property, propertyAccessExpression, path);
            }
        }

        if (property.HasNestedValidationWork)
        {
            AppendNestedValidationForProperty(builder, property, propertyAccessExpression, path);
        }

        if (property.HasCollectionValidationWork)
        {
            AppendCollectionValidationForProperty(builder, property, propertyAccessExpression, path);
        }
    }

    private void AppendNestedValidationForProperty(
        IndentedStringBuilder builder,
        PropertyModel property,
        string propertyAccessExpression,
        ValidationPath path)
    {
        ObjectModel objectModel = property.Object
            ?? throw new InvalidOperationException($"Nested validation requires object metadata for property '{property.Name}'.");
        ValueAccess access = objectModel.Shape.Renderer.Access(propertyAccessExpression);
        if (access.RequiresGuard)
        {
            builder.AppendBlock(
                $$"""
                if ({{access.GuardExpression}})
                {
                """);
            builder = builder.IncreaseIndent();
        }

        foreach (PropertyModel child in objectModel.Children)
        {
            AppendValidationForProperty(builder, child, $"{access.ValueExpression}.{child.Name}", path.AppendProperty(child.Name));
        }

        if (access.RequiresGuard)
        {
            builder = builder.DecreaseIndent();
            builder.AppendLine("}");
        }
    }

    private void AppendCollectionValidationForProperty(
        IndentedStringBuilder builder,
        PropertyModel property,
        string propertyAccessExpression,
        ValidationPath path)
    {
        CollectionModel collection = property.Collection!;
        string safeIdentifier = CreateSafeIdentifier(propertyAccessExpression);
        string collectionAccessExpression = propertyAccessExpression;
        string elementVariable = $"{safeIdentifier}Element";
        ValueAccess access = collection.Shape.Renderer.Access(propertyAccessExpression);

        if (access.RequiresGuard)
        {
            string collectionCurrentVariable = $"{safeIdentifier}CollectionCurrent";
            builder.AppendBlock(
                $$"""
                if ({{access.GuardExpression}})
                {
                    {{property.NonNullableTypeName}} {{collectionCurrentVariable}} = {{access.ValueExpression}};
                """);
            builder = builder.IncreaseIndent();
            collectionAccessExpression = collectionCurrentVariable;
        }

        string indexVariable = $"{safeIdentifier}Index";
        builder.AppendBlock(
            $$"""
            int {{indexVariable}} = 0;
            foreach ({{collection.ElementNullableTypeName}} {{elementVariable}} in {{collectionAccessExpression}})
            {
            """);
        IndentedStringBuilder loopBuilder = builder.IncreaseIndent();
        ValidationPath elementPath = path.AppendElement(indexVariable);

        if (property.TryGetAspects(out List<CollectionElementValidationAspect>? elementValidationAspects))
        {
            foreach (CollectionElementValidationAspect elementValidationAspect in elementValidationAspects)
            {
                elementValidationAspect.ValidationAspect.EmitCollectionElementValidation(
                    this,
                    loopBuilder,
                    property,
                    elementVariable,
                    elementPath);
            }
        }

        if (property.HasCollectionElementChildValidationWork)
        {
            AppendCollectionElementChildValidation(loopBuilder, property, elementVariable, elementPath);
        }
        loopBuilder.AppendLine($"++{indexVariable};");
        builder.AppendLine("}");

        if (access.RequiresGuard)
        {
            builder = builder.DecreaseIndent();
            builder.AppendLine("}");
        }
    }

    private void AppendCollectionElementChildValidation(
        IndentedStringBuilder builder,
        PropertyModel property,
        string elementVariable,
        ValidationPath elementPath)
    {
        if (property is not { Collection.ElementObject: { } elementObject })
        {
            throw new InvalidOperationException("Collection element child validation requires validatable object metadata.");
        }
        ValueAccess access = elementObject.Shape.Renderer.Access(elementVariable);
        if (access.RequiresGuard)
        {
            builder.AppendBlock(
                $$"""
                if ({{access.GuardExpression}})
                {
                """);
            builder = builder.IncreaseIndent();
        }

        foreach (PropertyModel child in elementObject.Children)
        {
            AppendValidationForProperty(builder, child, $"{access.ValueExpression}.{child.Name}", elementPath.AppendProperty(child.Name));
        }
        if (access.RequiresGuard)
        {
            builder = builder.DecreaseIndent();
            builder.AppendLine("}");
        }
    }

    private static string CreateSafeIdentifier(string value) => string.Concat(value.Select(static character => char.IsLetterOrDigit(character) ? character : '_'));

    public sealed record ValidationVariables(string Module, string Context, string Errors);
}
