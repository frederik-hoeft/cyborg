using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal sealed class DefaultInstanceFactoryAttributeProcessor : IPropertyAttributeProcessor
{
    public string AttributeMetadataName => typeof(DefaultInstanceFactoryAttribute).FullName;

    public bool TryProcess(PropertyProcessingContext context, AttributeData attribute, out PropertyValidationAspect? aspect)
    {
        aspect = null;

        if (attribute.ConstructorArguments.Length == 0)
        {
            context.Report(
                ValidationGeneratorDiagnostics.MissingArgument,
                context.Property.Name,
                context.ContainingType.Name,
                nameof(DefaultInstanceFactoryAttribute));
            return false;
        }

        if (attribute.ConstructorArguments[0].Value is not string valueExpression)
        {
            context.Report(
                ValidationGeneratorDiagnostics.UnsupportedAttributeLiteral,
                context.Property.Name,
                context.ContainingType.Name);
            return false;
        }

        IEnumerable<IMethodSymbol> candidateMethods = context.ContainingType
            .GetMembers(valueExpression)
            .OfType<IMethodSymbol>();

        IMethodSymbol? factoryMethod = candidateMethods.FirstOrDefault(method => IsCompatibleFactoryMethod(context, method));
        if (factoryMethod is null)
        {
            context.Report(
                ValidationGeneratorDiagnostics.FactoryMemberSignatureMismatch,
                context.Property.Name,
                context.ContainingType.Name,
                nameof(DefaultInstanceFactoryAttribute),
                valueExpression,
                context.Property.Type);
            return false;
        }

        aspect = new DefaultInstanceFactoryAspect(valueExpression);
        return true;
    }

    private static bool IsCompatibleFactoryMethod(PropertyProcessingContext context, IMethodSymbol method)
    {
        if (method is not { IsGenericMethod: false, Parameters: [] })
        {
            return false;
        }

        ITypeSymbol returnType = method.ReturnType;
        ITypeSymbol propertyType = context.Property.Type;

        return SymbolEqualityComparer.Default.Equals(returnType, propertyType)
            || context.Compilation.ClassifyConversion(returnType, propertyType).IsImplicit;
    }

    private sealed class DefaultInstanceFactoryAspect(string factoryMember) : PropertyValidationAspect
    {
        public override bool EnsuresDefault => true;

        public override string? RewriteDefaultAssignmentExpression(PropertyRewriteContext context, string? currentExpression)
        {
            _ = currentExpression;
            return $"{context.PropertyAccessExpression} is null ? {factoryMember}() : {context.PropertyAccessExpression}";
        }
    }
}
