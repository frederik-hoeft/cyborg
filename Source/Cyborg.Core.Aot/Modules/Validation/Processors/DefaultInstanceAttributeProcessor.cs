using Cyborg.Core.Aot.Modules.Validation.Model;
using Cyborg.Core.Aot.Modules.Validation.Rendering;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal sealed class DefaultInstanceAttributeProcessor : IPropertyAttributeProcessor
{
    public string AttributeMetadataName => typeof(DefaultInstanceAttribute).FullName;

    public bool TryProcess(PropertyAttributeProcessingContext context, AttributeData attribute, out PropertyValidationAspect? aspect)
    {
        _ = attribute;
        aspect = null;

        ITypeSymbol propertyType = NormalizePropertyType(context.Property.Type);

        if (propertyType is not INamedTypeSymbol namedPropertyType || namedPropertyType.TypeKind == TypeKind.Interface)
        {
            context.Report(
                ValidationGeneratorDiagnostics.UnsupportedDefaultInstanceTargetType,
                context.Property.Name,
                context.ContainingType.Name,
                context.Property.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));

            return false;
        }
        aspect = new DefaultInstanceValidationAspect(context.ContainingType, namedPropertyType, context.Property);
        return true;
    }

    private static ITypeSymbol NormalizePropertyType(ITypeSymbol propertyType) =>
        // For this attribute we only care about stripping nullable *reference* annotation.
        // Nullable<T> value types are invalid anyway because IDefaultInstance<TSelf> has `where TSelf : class`.
        propertyType.WithNullableAnnotation(NullableAnnotation.None);

    private sealed class DefaultInstanceValidationAspect(INamedTypeSymbol containingType, INamedTypeSymbol propertyType, IPropertySymbol property) : PropertyValidationAspect
    {
        public override bool EnsuresDefault => true;

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
                if (!iface.IsGenericType || iface.TypeArguments is not [{ } self])
                {
                    continue;
                }
                if (!SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, contractInfo.IDefaultValueT))
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