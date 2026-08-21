using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal sealed class UntaggedProcessor : AttributeProcessorBase<UntaggedAttribute>
{
    public override bool TryProcess(AttributeData attribute, ref readonly PropertyProcessingContext context, out PropertyAspect? aspect)
    {
        if (context.Property.HasAttribute<SecretAttribute>())
        {
            context.Report(
                ValidationGeneratorDiagnostics.SecretAndUntaggedAreMutuallyExclusive,
                context.Property.Name,
                context.ContainingType.Name);
            return false.WithDefaults(out aspect);
        }
        if (!context.Property.Type.EqualsIgnoreNullability(SpecialType.System_String))
        {
            context.Report(
                ValidationGeneratorDiagnostics.UntaggedRequiresString,
                context.Property.Name,
                context.ContainingType.Name,
                context.Property.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
            return false.WithDefaults(out aspect);
        }
        aspect = new UntaggedAspect();
        return true;
    }
}

internal sealed class UntaggedAspect : PropertyAspect;
