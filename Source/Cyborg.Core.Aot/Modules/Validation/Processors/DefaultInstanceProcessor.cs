using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Aot.Modules.Validation.Rendering;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal sealed class DefaultInstanceProcessor : AttributeProcessorBase<DefaultInstanceAttribute>
{
    public override bool TryProcess(AttributeData attribute, ref readonly PropertyProcessingContext context, out PropertyAspect? aspect)
    {
        ITypeSymbol propertyType = NormalizePropertyType(context.Property.Type);

        if (propertyType is not INamedTypeSymbol namedPropertyType || namedPropertyType.TypeKind == TypeKind.Interface)
        {
            context.Report(
                ValidationGeneratorDiagnostics.UnsupportedDefaultInstanceTargetType,
                context.Property.Name,
                context.ContainingType.Name,
                context.Property.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
            return false.WithDefaults(out aspect);
        }
        aspect = new DefaultInstanceValidationAspect(context.ContainingType, namedPropertyType, context.Property);
        return true;
    }

    private static ITypeSymbol NormalizePropertyType(ITypeSymbol propertyType) =>
        // For this attribute we only care about stripping nullable *reference* annotation.
        // Nullable<T> value types are invalid anyway because IDefaultInstance<TSelf> has `where TSelf : class`.
        propertyType.WithNullableAnnotation(NullableAnnotation.None);

    private sealed class DefaultInstanceValidationAspect(INamedTypeSymbol containingType, INamedTypeSymbol propertyType, IPropertySymbol property) : PropertyAspect(ensuresDefault: true)
    {
        public override string? RewriteDefaultAssignmentExpression(PropertyRewriteContext context, string? currentExpression)
        {
            if (!ImplementsMatchingDefaultInstanceInterface(propertyType, context.ContractInfo))
            {
                context.DiagnosticsReporter.Report(
                    ValidationGeneratorDiagnostics.UnsupportedDefaultInstanceTargetType,
                    property.Locations.FirstOrDefault() ?? Location.None,
                    context.Property.Name,
                    containingType.Name,
                    propertyType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
                return null;
            }

            string nonNullableTypeName = propertyType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            _ = currentExpression;
            return $"{context.PropertyAccessExpression} is null ? {ModuleValidationRenderer.Helpers}.{ModuleValidationRenderer.HelperMembers.GetDefaultInstance}<{nonNullableTypeName}>() : {context.PropertyAccessExpression}";
        }

        private static bool ImplementsMatchingDefaultInstanceInterface(INamedTypeSymbol propertyType, ValidationContractInfo contractInfo)
        {
            foreach (INamedTypeSymbol iface in propertyType.AllInterfaces)
            {
                if (iface is not { IsGenericType: true, TypeArguments: [{ } self] } || !SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, contractInfo.IDefaultValueT))
                {
                    continue;
                }
                if (SymbolEqualityComparer.Default.Equals(self, propertyType))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
