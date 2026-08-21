using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Aot.Modules.Validation.Models;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal abstract class PropertyValidationProcessorBase<TAttribute> : AttributeProcessorBase<TAttribute> where TAttribute : PropertyValidationAttribute
{
    public sealed override bool TryProcess(AttributeData attribute, ref readonly PropertyProcessingContext context, out PropertyAspect? aspect)
    {
        bool targetsElements = false;
        if (TryGetNamedArgument(attribute, nameof(PropertyValidationAttribute.TargetsElements), out TypedConstant? targetsElementsArgument))
        {
            if (targetsElementsArgument.Value.Value is not bool typedTargetsElements)
            {
                context.Report(ValidationGeneratorDiagnostics.UnsupportedAttributeLiteral,
                    context.Property.Name,
                    context.ContainingType.Name,
                    GetAttributeFriendlyName(attribute));
                return false.WithDefaults(out aspect);
            }
            targetsElements = typedTargetsElements;
        }

        PropertyValidationTarget target;
        if (targetsElements)
        {
            if (!TryCreateCollectionElementTarget(attribute, in context, out target))
            {
                return false.WithDefaults(out aspect);
            }
        }
        else
        {
            target = new PropertyValidationTarget(context.Property.Type, IsCollectionElement: false);
        }
        if (!TryProcessValidation(attribute, in context, in target, out PropertyValidationAspect? validationAspect))
        {
            return false.WithDefaults(out aspect);
        }
        _ = validationAspect ?? throw new InvalidOperationException($"Processor '{GetType().FullName}' returned success without a validation aspect.");

        aspect = targetsElements
            ? new CollectionElementValidationAspect(validationAspect)
            : validationAspect;
        return true;
    }

    protected abstract bool TryProcessValidation(AttributeData attribute, ref readonly PropertyProcessingContext context, ref readonly PropertyValidationTarget target, out PropertyValidationAspect? aspect);

    protected bool ValidateTargetType(AttributeData attribute, ref readonly PropertyProcessingContext context, ref readonly PropertyValidationTarget target, SpecialType expectedType)
    {
        if (!target.IsCollectionElement)
        {
            return ValidatePropertyType(attribute, in context, expectedType);
        }
        if (expectedType is SpecialType.None)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedType));
        }
        if (target.Type.SpecialType == expectedType)
        {
            return true;
        }

        context.Report(ValidationGeneratorDiagnostics.CollectionElementTypeMismatch,
            context.Property.Name,
            context.ContainingType.Name,
            GetAttributeFriendlyName(attribute),
            target.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            context.Compilation.GetSpecialType(expectedType).ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
        return false;
    }

    protected bool ValidateStringLikeTargetType(AttributeData attribute, ref readonly PropertyProcessingContext context, ref readonly PropertyValidationTarget target)
    {
        if (!target.IsCollectionElement)
        {
            return ValidateStringLikePropertyType(attribute, in context);
        }
        if (target.Type.IsStringLike(context.ContractInfo.TaggedString))
        {
            return true;
        }

        context.Report(ValidationGeneratorDiagnostics.CollectionElementTypeMismatch,
            context.Property.Name,
            context.ContainingType.Name,
            GetAttributeFriendlyName(attribute),
            target.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            "string or TaggedString");
        return false;
    }

    private bool TryCreateCollectionElementTarget(AttributeData attribute, ref readonly PropertyProcessingContext context, out PropertyValidationTarget target)
    {
        if (!CollectionTypeInspector.TryDescribe(
            context.Compilation,
            context.Property.Type,
            out CollectionShape? shape))
        {
            context.Report(
                ValidationGeneratorDiagnostics.CollectionApplicationRequiresCollection,
                context.Property.Name,
                context.ContainingType.Name,
                GetAttributeFriendlyName(attribute),
                context.Property.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
            target = default;
            return false;
        }

        target = new PropertyValidationTarget(shape.ElementType, IsCollectionElement: true);
        return true;
    }
}
