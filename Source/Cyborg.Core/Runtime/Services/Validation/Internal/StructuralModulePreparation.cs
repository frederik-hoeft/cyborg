using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Engine.Expressions;
using Cyborg.Core.Runtime.Engine.Expressions.Bound;
using Cyborg.Core.Runtime.Engine.Expressions.Evaluated;
using Cyborg.Core.Text;

namespace Cyborg.Core.Runtime.Services.Validation.Internal;

/// <summary>
/// Owns expressions for one generated validation invocation. Record and collection rewrites retain their ordinary CLR types;
/// their logical member paths identify the immutable expression values carried between preparation stages.
/// </summary>
internal sealed class StructuralModulePreparation(RuntimeEnvironment environment)
{
    private readonly Dictionary<string, BoundValue> _selectedOverrides = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PreparedExpression> _prepared = new(StringComparer.Ordinal);
    private readonly ExpressionMaterializer _materializer = new();

    private sealed record PreparedExpression(BoundValue? PendingText, bool Terminal);

    public bool TrySelectOverride<TModule>(TModule module, string moduleExpression, string valueExpression, string preparationKey, out object? value)
        where TModule : ModuleBase, IModuleDefinition
    {
        if (!environment.TrySelectBoundOverride(module, moduleExpression, valueExpression, out BoundValue? selected))
        {
            value = null;
            return false;
        }
        _selectedOverrides[preparationKey] = selected;
        value = selected.PresentStoredValue();
        return true;
    }

    public object? ResolveValueExpression(object value, bool willInterpolate, string preparationKey)
    {
        BoundValue bound = _selectedOverrides.Remove(preparationKey, out BoundValue? selected)
            ? selected
            : environment.BindStructuralValue(value);

        if (!bound.IsTextual)
        {
            EvaluatedValue evaluated = environment.EvaluateStructuralValue(bound);
            _prepared[preparationKey] = new PreparedExpression(PendingText: null, Terminal: true);
            return _materializer.Materialize(evaluated);
        }

        if (!willInterpolate)
        {
            _prepared.Remove(preparationKey);
            BoundValue textValue = bound is BoundWithTags wrapper ? wrapper.Inner : bound;
            if (textValue is BoundText text)
            {
                string finalized = text.RenderWithoutInterpolation();
                return value is TaggedString taggedValue ? taggedValue.WithValue(finalized) : finalized;
            }
            return value;
        }

        _prepared[preparationKey] = new PreparedExpression(PendingText: bound, Terminal: false);
        return value;
    }

    public TaggedString Interpolate(object value, string preparationKey)
    {
        if (_prepared.Remove(preparationKey, out PreparedExpression? prepared))
        {
            if (prepared.Terminal)
            {
                // The typed result was already materialized for destination invariants; do not reinterpret its characters.
                return ToTaggedString(value);
            }
            return EvaluateText(prepared.PendingText!, value);
        }

        // [IgnoreValueExpression] leaves this as a separate, explicit text-input boundary.
        // Binding textual syntax does not capture; evaluation uses the current runtime environment.
        BoundValue bound = environment.BindStructuralText(value is TaggedString tagged ? tagged.Value : (string)value);
        return EvaluateText(bound, value);
    }

    private TaggedString EvaluateText(BoundValue bound, object currentValue)
    {
        object? materialized = _materializer.Materialize(environment.EvaluateStructuralValue(bound));
        TaggedString result = ToTaggedString(materialized);
        if (currentValue is TaggedString original && original.HasTags)
        {
            return result.WithTags(original.Tags);
        }
        return result;
    }

    private static TaggedString ToTaggedString(object? value) => value switch
    {
        TaggedString tagged => tagged,
        string text => new TaggedString(text),
        null => throw new InvalidCastException("A textual expression resolved to null rather than text."),
        _ => throw new InvalidCastException($"A textual expression resolved to '{value.GetType().FullName}' rather than text.")
    };

}
