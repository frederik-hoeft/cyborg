using Cyborg.Core.Aot.Extensions;
using Microsoft.CodeAnalysis;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal abstract class AttributeProcessorBase : IPropertyAttributeProcessor
{
    private static readonly SymbolDisplayFormat s_friendlyTypeNameFormat = SymbolDisplayFormat.MinimallyQualifiedFormat.AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    public abstract string AttributeMetadataName { get; }

    protected virtual string GetAttributeFriendlyName(AttributeData attribute) => attribute.AttributeClass?.Name ?? "Unknown";

    public abstract bool TryProcess(AttributeData attribute, ref readonly PropertyProcessingContext context, out PropertyAspect? aspect);

    protected bool ValidateTypeArguments(AttributeData attribute, ref readonly PropertyProcessingContext context, params ReadOnlySpan<ITypeSymbol> expectedTypeArguments)
    {
        INamedTypeSymbol attributeClass = attribute.AttributeClass
            ?? throw new InvalidOperationException($"Internal error: Attribute class is null for attribute {attribute}");
        if (expectedTypeArguments.Length != attributeClass.TypeArguments.Length)
        {
            throw new InvalidOperationException($"Internal error: Attribute {attributeClass} has {attributeClass.TypeArguments.Length} type arguments, but {expectedTypeArguments.Length} were expected.");
        }
        bool isSuccess = true;
        for (int i = 0; i < expectedTypeArguments.Length; ++i)
        {
            if (!SymbolEqualityComparer.Default.Equals(expectedTypeArguments[i], attributeClass.TypeArguments[i]))
            {
                context.Report(ValidationGeneratorDiagnostics.GenericTypesMismatch, context.Property.Name, context.ContainingType.Name, GetAttributeFriendlyName(attribute), i, expectedTypeArguments[i].Name);
                isSuccess = false;
            }
        }
        return isSuccess;
    }

    protected bool TryGetConstructorArgumentExpression(AttributeData attribute, int argumentIndex, ref readonly PropertyProcessingContext context, [NotNullWhen(true)] out string? expression)
    {
        if (!TryGetConstructorArgument(attribute, argumentIndex, in context, out TypedConstant? argument))
        {
            return false.WithDefaults(out expression);
        }
        if (!LiteralExpressionFactory.TryGetLiteralExpression(argument.Value, context.Property.Type, out expression))
        {
            context.Report(
                ValidationGeneratorDiagnostics.UnsupportedAttributeLiteral,
                context.Property.Name,
                context.ContainingType.Name,
                GetAttributeFriendlyName(attribute));
            return false;
        }
        return true;
    }

    protected bool TryGetConstructorArgumentValue<T>(AttributeData attribute, int argumentIndex, ref readonly PropertyProcessingContext context, [NotNullWhen(true)] out T? value)
    {
        if (!TryGetConstructorArgument(attribute, argumentIndex, in context, out TypedConstant? argument))
        {
            return false.WithDefaults(out value);
        }
        if (argument.Value is not { IsNull: false, Value: T argumentValue })
        {
            context.Report(
                ValidationGeneratorDiagnostics.UnsupportedAttributeLiteral,
                context.Property.Name,
                context.ContainingType.Name,
                GetAttributeFriendlyName(attribute));
            return false.WithDefaults(out value);
        }
        value = argumentValue;
        return true;
    }

    protected bool TryGetConstructorArgument(AttributeData attribute, int argumentIndex, ref readonly PropertyProcessingContext context, [NotNullWhen(true)] out TypedConstant? value)
    {
        if (attribute.ConstructorArguments.Length <= argumentIndex)
        {
            context.Report(
                ValidationGeneratorDiagnostics.MissingArgument,
                context.Property.Name,
                context.ContainingType.Name,
                GetAttributeFriendlyName(attribute));
            return false.WithDefaults(out value);
        }
        value = attribute.ConstructorArguments[argumentIndex];
        return true;
    }

    protected bool TryGetNamedArgumentExpressions(AttributeData attribute, ref readonly PropertyProcessingContext context, [NotNullWhen(true)] out Dictionary<string, string?> namedArguments)
    {
        namedArguments = new Dictionary<string, string?>(capacity: attribute.NamedArguments.Length);
        foreach (KeyValuePair<string, TypedConstant> named in attribute.NamedArguments)
        {
            if (!named.Value.IsNull)
            {
                if (!LiteralExpressionFactory.TryGetLiteralExpression(named.Value, context.Property.Type, out string? expression))
                {
                    context.Report(
                        ValidationGeneratorDiagnostics.UnsupportedAttributeLiteral,
                        context.Property.Name,
                        context.ContainingType.Name,
                        GetAttributeFriendlyName(attribute));
                    return false;
                }
                namedArguments[named.Key] = expression;
            }
        }
        return true;
    }

    protected bool TryGetNamedArgumentValue<T>(AttributeData attribute, string argumentName, ref readonly PropertyProcessingContext context, [NotNullWhen(true)] out T? value)
    {
        if (TryGetNamedArgument(attribute, argumentName, out TypedConstant? namedArgumentValue))
        {
            if (namedArgumentValue.Value.Value is T typedValue)
            {
                value = typedValue;
                return true;
            }
            context.Report(ValidationGeneratorDiagnostics.UnsupportedAttributeLiteral,
                context.Property.Name,
                context.ContainingType.Name,
                GetAttributeFriendlyName(attribute));
        }
        return false.WithDefaults(out value);
    }

    protected bool TryGetNamedArgument(AttributeData attribute, string argumentName, [NotNullWhen(true)] out TypedConstant? value)
    {
        foreach (KeyValuePair<string, TypedConstant> namedArgument in attribute.NamedArguments)
        {
            if (namedArgument.Key.Equals(argumentName, StringComparison.InvariantCulture))
            {
                value = namedArgument.Value;
                return true;
            }
        }
        return false.WithDefaults(out value);
    }

    protected bool ValidatePropertyType(AttributeData attribute, ref readonly PropertyProcessingContext context, SpecialType expectedType)
    {
        if (expectedType is SpecialType.None)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedType));
        }

        if (context.Property.Type.SpecialType != expectedType)
        {
            context.Report(ValidationGeneratorDiagnostics.TypeMismatch,
                context.Property.Name,
                context.ContainingType.Name,
                GetAttributeFriendlyName(attribute),
                context.Compilation.GetSpecialType(expectedType).ToDisplayString(s_friendlyTypeNameFormat));
            return false;
        }
        return true;
    }
}
