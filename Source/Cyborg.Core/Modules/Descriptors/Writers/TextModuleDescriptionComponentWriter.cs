using Cyborg.Core.Common.Text;
using Cyborg.Core.Modules.Descriptors.Model;
using System.Collections.Immutable;

namespace Cyborg.Core.Modules.Descriptors.Writers;

public sealed class TextModuleDescriptionComponentWriter(IndentedStringBuilder builder) : IDescriptionComponentWriter
{
    public ValueTask WriteAtomAsync<T>(T value, ImmutableArray<string> hints, CancellationToken cancellationToken)
    {
        builder.AppendLine(value?.ToString() ?? "null");
        return ValueTask.CompletedTask;
    }

    public async ValueTask WriteAsync(IDescriptionObjectComponent objectComponent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(objectComponent);
        builder.GetInnerBuilder().AppendLine();
        builder.IncreaseIndent();
        foreach (IDescriptionPropertyComponent property in objectComponent.Properties)
        {
            await property.AcceptAsync(this, cancellationToken);
        }
    }

    public async ValueTask WriteAsync(IDescriptionCollectionComponent collectionComponent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(collectionComponent);
        builder.GetInnerBuilder().AppendLine();
        builder.IncreaseIndent();
        foreach (IDescriptionValueComponent item in collectionComponent.Items)
        {
            builder.Append("- ");
            await item.AcceptAsync(this, cancellationToken);
        }
    }

    public ValueTask WriteAsync(IDescriptionValueComponent valueComponent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(valueComponent);
        return valueComponent.AcceptAsync(this, cancellationToken);
    }

    public async ValueTask WriteAsync(IDescriptionPropertyComponent propertyComponent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(propertyComponent);
        builder.Append($"{propertyComponent.Name}: ");
        await propertyComponent.Value.AcceptAsync(this, cancellationToken);
    }
}
