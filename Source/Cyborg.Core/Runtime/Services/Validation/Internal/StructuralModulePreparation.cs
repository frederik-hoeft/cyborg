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
    private readonly Dictionary<string, SelectedBoundOverride> _selectedOverrides = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PreparedExpression> _prepared = new(StringComparer.Ordinal);
    private readonly ExpressionMaterializer _materializer = new();

    private sealed record PreparedExpression(SelectedBoundOverride? PendingText, bool Terminal);

    public bool TrySelectOverride<TModule>(TModule module, string moduleExpression, string valueExpression, string preparationKey, out object? value)
        where TModule : ModuleBase, IModuleDefinition
    {
        if (!environment.TrySelectBoundOverride(module, moduleExpression, valueExpression, out SelectedBoundOverride? selected))
        {
            value = null;
            return false;
        }
        _selectedOverrides[preparationKey] = selected;
        value = selected.Value.PresentStoredValue();
        return true;
    }

    public void DiscardSelectedOverride(string preparationKey) => _selectedOverrides.Remove(preparationKey);

    public object? ResolveValueExpression(object value, bool willInterpolate, string preparationKey)
    {
        SelectedBoundOverride expression = _selectedOverrides.Remove(preparationKey, out SelectedBoundOverride? selected)
            ? selected
            : new SelectedBoundOverride(environment.BindStructuralValue(value), environment);
        BoundValue bound = expression.Value;

        if (!bound.IsTextual)
        {
            EvaluatedValue evaluated = expression.Source.EvaluateStructuralValue(bound, environment);
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

        _prepared[preparationKey] = new PreparedExpression(PendingText: expression, Terminal: false);
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
        return EvaluateText(new SelectedBoundOverride(bound, environment), value);
    }

    private TaggedString EvaluateText(SelectedBoundOverride expression, object currentValue)
    {
        object? materialized = _materializer.Materialize(expression.Source.EvaluateStructuralValue(expression.Value, environment));
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
