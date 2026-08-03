using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal sealed class DefaultInstanceFactoryProcessor : AttributeProcessorBase<DefaultInstanceFactoryAttribute>
{
    public override bool TryProcess(AttributeData attribute, ref readonly PropertyProcessingContext context, out PropertyAspect? aspect)
    {
        if (!TryGetConstructorArgumentValue(attribute, argumentIndex: 0, in context, out string? valueExpression))
        {
            return false.WithDefaults(out aspect);
        }
        IEnumerable<IMethodSymbol> candidateMethods = context.ContainingType
            .GetMembers(valueExpression)
            .OfType<IMethodSymbol>();
        (Compilation compilation, _, IPropertySymbol? property, _) = context;
        IMethodSymbol? factoryMethod = candidateMethods.FirstOrDefault(method => IsCompatibleFactoryMethod(property.Type, compilation, method));
        if (factoryMethod is null)
        {
            context.Report(
                ValidationGeneratorDiagnostics.FactoryMemberSignatureMismatch,
                context.Property.Name,
                context.ContainingType.Name,
                nameof(DefaultInstanceFactoryAttribute),
                valueExpression,
                context.Property.Type);
            return false.WithDefaults(out aspect);
        }

        aspect = new DefaultInstanceFactoryAspect(valueExpression);
        return true;
    }

    private static bool IsCompatibleFactoryMethod(ITypeSymbol propertyType, Compilation compilation, IMethodSymbol method)
    {
        if (method is not { IsGenericMethod: false, Parameters: [] })
        {
            return false;
        }
        ITypeSymbol returnType = method.ReturnType;
        return SymbolEqualityComparer.Default.Equals(returnType, propertyType)
            || compilation.ClassifyConversion(returnType, propertyType).IsImplicit;
    }

    private sealed class DefaultInstanceFactoryAspect(string factoryMember) : PropertyAspect(ensuresDefault: true)
    {
        public override string? RewriteDefaultAssignmentExpression(PropertyRewriteContext context, string? currentExpression)
        {
            _ = currentExpression;
            return $"{context.PropertyAccessExpression} is null ? {factoryMember}() : {context.PropertyAccessExpression}";
        }
    }
}
