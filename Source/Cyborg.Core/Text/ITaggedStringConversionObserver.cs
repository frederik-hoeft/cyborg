namespace Cyborg.Core.Text;

/// <summary>
/// Observes implicit <see cref="TaggedString"/> to <see cref="string"/> conversions during environment retrieval.
/// </summary>
public interface ITaggedStringConversionObserver
{
    void OnImplicitStringRetrieval(string variableName, TaggedString value);
}
