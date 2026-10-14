# Interpolation and Override Resolution

This document defines the runtime contract for storing value expressions, resolving variables, selecting module-property overrides, applying generated interpolation, and explicitly interpolating deferred values. It also defines the escape syntax for preserving literal operator expressions.

The design separates **selection** from **evaluation**, and separates **text** from **typed values**. Value expressions are parsed into immutable syntax, bound to their definition context, evaluated structurally against a resolution view, and materialized as CLR values only at consumer boundaries. An exact `&{...}` value remains lazy until it is read; an exact `*{...}` value is snapshotted when defined. Generated module preparation may select overrides without evaluating them, and explicit resolution APIs remain complete materialization boundaries.

Key grammar is not value grammar. Override addresses and other keys continue to support only interpolation, for example `@${my_module}.property`. Lazy indirection and eager capture are rejected in keys and in any other textual interpolation.

## Expression Syntax

| Syntax | Meaning |
|--------|---------|
| `${identifier}` | Interpolate `identifier` relative to the scope where the expression is encountered. The result is text. |
| `${@identifier}` | Interpolate `identifier` relative to the original resolution entry point. |
| `${@}` | Interpolate the current scope's namespace. |
| `${@@}` | Interpolate the original entry point's namespace. |
| `&{identifier}` | Lazy typed indirection. The entire value is a reference; each read yields the target's current type and value. |
| `&{@identifier}` | Lazy typed indirection resolved from the original entry point. |
| `&{@}` / `&{@@}` | Lazy typed references to the current scope namespace and the entry-point namespace. |
| `*{identifier}` | Eager typed capture. The target is resolved once when the variable is defined, and the variable stores that value. |

`&{...}` and `*{...}` are valid only when they are the entire value. `*{@...}` is reserved and invalid. `${@}` and `${@@}` remain string interpolation, including inside larger text.

Unresolved `${...}` placeholders remain unchanged. Undefined targets of `&{...}` and `*{...}` fail with `InvalidOperationException`. Cyclic references fail with `InvalidOperationException`. A value whose CLR type is not the requested type fails with `InvalidCastException`.

## Literal Escape Syntax

A `#` immediately after `${`, `&{`, or `*{` marks a final-phase literal. Each evaluation pass removes exactly one leading `#` and does not rescan the expression that removal reveals.

| Input | Result after one pass |
|-------|------------------------|
| `${#HOME}` | `${HOME}` |
| `${##HOME}` | `${#HOME}` |
| `&{#port}` | `&{port}` |
| `*{#port}` | `*{port}` |
| `*{#@port}` | `*{@port}` |

`&{#port}` and `*{#port}` are not indirection or capture. They are ordinary text until a pass finalizes them. An unescaped `&{...}` or `*{...}` with leading or trailing text is a syntax error (`FormatException`), not a literal.

## Runtime Phases

### Structural representation

The grammar composes specialized parsers from `Cyborg.Core.Parsing`. Transient parser syntax nodes are collected into an immutable syntax representation. Binding creates immutable stored nodes: textual literals and interpolations, lazy typed references, eager terminal snapshots, and wrappers around ordinary CLR values all participate in the same value model. Evaluation produces a separate immutable terminal structure, preserving interpolated splices, escape depth, and tag propagation without reinterpreting the resulting text. Materialization is an explicit boundary and never changes the stored expression.

Environment inheritance, copying, and transactional forks preserve bound nodes internally. Public enumeration deliberately presents the *stored* CLR form for compatibility, not a resolved snapshot; resolved values are available through `TryResolveVariable(...)`. The environment does not treat public enumeration as a structural transfer mechanism.

### 1. Storage

`SetVariable(...)` is the definition boundary.

- `*{identifier}` resolves the target immediately and stores a snapshot of its type and value. The expression text is not retained. The snapshot is the reference returned by that resolution; Cyborg does not deep-clone it. A missing target fails the write. `*{@...}` is rejected.
- `&{...}` is stored as an immutable lazy reference to the target and its resolution-origin semantics.
- Other textual inputs are compiled into bound structural text after rejecting embedded unescaped `&{...}` and `*{...}`. `${...}` inside them is not evaluated yet.
- Ordinary CLR values are stored inside terminal bound nodes, not mixed indiscriminately with internal expression objects.

This preserves forward references and entry-point-sensitive `${@...}` / `${@@}` behavior for interpolation, and keeps indirection late-bound.

### 2. Variable resolution

`TryResolveVariable(...)` evaluates a variable from the caller's entry point.

- A captured snapshot is returned as stored. It is not interpolated again, and a further escape layer is not removed.
- An exact `&{...}` reference is resolved recursively to the target's current value. Tags on a `TaggedString` wrapper are unioned onto a textual target.
- Other strings are interpolated. `${...}` references are resolved with the same scope rules. Missing interpolation targets stay in the result. String results then remove one escape layer.
- Non-string values are returned as stored.

An exact `${port}` whose target is an `int` therefore becomes the text `"22"` (or whatever `ToString` produces). The integer is available only through `&{port}` or through a capture of `port`.

For ordinary textual variables, resolution preserves the source CLR type: stored `string` values remain strings, and stored `TaggedString` values remain `TaggedString` even if they have no tags. When interpolation introduces tags from an operand, the result is promoted to `TaggedString`. Typed indirection and capture preserve that resulting type as well as any tags.

When a required argument is resolved and subsequently bound into another Cyborg environment, its terminal structural value is transferred without turning already-finalized text into a new live expression.

### 3. Module-property override selection

Generated preparation separates override selection from value-expression evaluation:

- **Textual properties:** the generated validation support context selects the first matching stored override without evaluating its contents. For Cyborg runtime environments, a per-validation structural preparation state retains the selected expression and its `TaggedString` tags through subsequent phases. Captured snapshots remain terminal without adding escape characters.
- **Non-text properties:** the context performs full typed resolution. `&{...}` yields the current target and a capture yields the snapshot taken at definition. Collections use the collection-specific resolver before generated code materializes the declared collection shape. Capture of a collection is shallow; its elements are not recursively terminalized.

Raw textual selection is required so `[IgnoreInterpolation]` applies to the effective value regardless of whether it came from JSON, a default, or an override. It also keeps override precedence/selection independent from the semantics of the selected text.

These operations are not part of the normal worker-facing environment API. Source-generated preparation code accesses them through `ModuleValidationContext` in the `Cyborg.Core.Runtime.Services.Validation.Internal` namespace. For `RuntimeEnvironment`, the context holds invocation-local structural preparation state across the generated phases. Legacy raw-CLR methods remain for compatibility callers and other `IRuntimeEnvironment` implementations. Those CLR-only signatures cannot carry terminality metadata and still use limited compatibility shielding when a captured value would otherwise be reinterpreted by a later textual pass. This IntelliSense-hidden CLR bridge carries the runtime and service provider required by the generated phases, while the corresponding environment operations remain internal interface members.

Typed override resolution is therefore a generated-pipeline concern rather than a client-code API. Module workers use the ordinary environment operations described under [API Boundaries](#api-boundaries).

### 4. Generated value-expression preparation

After override selection, generated preparation recursively visits textual properties and textual elements in supported collections. A whole-value `&{...}` or `*{...}` is resolved as a typed value expression; `${...}` remains untouched for the later textual phase. The same pass applies to directly configured values and values supplied through overrides, so string-valued properties do not have a separate indirection model.

The pass is independent from `[IgnoreInterpolation]`: suppressing `${...}` interpolation does not suppress typed indirection or capture. Destination preparation invariants are applied again after this phase, so attributes such as `[Secret]` cannot be bypassed when a reference replaces the effective property value.

`ModuleBase.Name` and `ModuleBase.Group` opt out through `[IgnoreValueExpression]`. Their structural identity is consumed when the runtime binds the module environment before generated validation begins; rewriting those fields afterward would make the prepared module disagree with the namespace already selected for execution.

### 5. Generated interpolation

The generated validation pipeline performs:

1. apply defaults and preparation invariants;
2. select or resolve overrides;
3. apply defaults again to values introduced by overrides;
4. resolve typed value expressions in textual properties;
5. re-establish destination preparation invariants without applying more defaults;
6. interpolate eligible strings through the generated validation context;
7. validate constraints.

The generated interpolation operation evaluates structural `${...}` expressions and removes one escape layer structurally, without reparsing an already-evaluated result. It is applied recursively to eligible string properties in nested `[Validatable]` records and supported collections. It rejects active `&{...}` and `*{...}` because those belong to the preceding value-expression phase.

Properties marked `[IgnoreInterpolation]` skip only this phase. A `${...}` template therefore remains available for worker-controlled interpolation, while a whole-value `&{...}` still resolves during value-expression preparation. A worker that later calls `Interpolate` starts a new textual pass.

### 6. Explicit and deferred interpolation

Module workers and other handwritten consumers use one interpolation API:

```csharp
TaggedString result = runtime.Environment.Interpolate(value);
string raw = result.Value; // execution-facing raw string
```

`Interpolate(...)` and `TryResolveVariable(...)` are complete evaluation boundaries for text. They resolve ordinary expressions recursively and remove one escape layer in string results. Interpolation returns a `TaggedString` whose tags are the union of the template's tags and the tags of every successfully resolved interpolation operand. Retrieving a tagged result as `string` still yields the raw value for compatibility, but discards tags; prefer `TryResolveVariable(..., out TaggedString)`.

A worker should manually interpolate only when evaluation was intentionally deferred until worker execution, normally through `[IgnoreInterpolation]`. Eligible properties processed by the generated pipeline are already interpolated before the worker receives the validated module and should not be interpolated again.

Each explicit `Interpolate(...)` call is a distinct pass, so layered escapes can intentionally survive one or more calls:

```text
Interpolate("${##HOME}") -> "${#HOME}"
Interpolate("${#HOME}")  -> "${HOME}"
Interpolate("${HOME}")   -> resolved HOME value, when defined
```

## API Boundaries

The environment API exposed to module authors includes operations that are meaningful during handwritten execution, such as:

- `Interpolate(...)` for intentionally deferred string evaluation (returns `TaggedString` so tags union);
- `TryResolveVariable(...)` for typed variable reads, preferably as `TaggedString`;
- `SetVariable(...)` and `TryRemoveVariable(...)` for environment state. `SetVariable` evaluates eager capture and rejects illegal value syntax.

Generated preparation additionally requires raw string override selection, typed scalar and collection override materialization, and access to the runtime and service provider shared by every preparation phase. These operations are grouped on `ModuleValidationContext` rather than exposed as public members of `IRuntimeEnvironment`. The context must be public because generated code is compiled into consuming assemblies, but it has a private constructor, lives in an `Internal` namespace, and is marked as editor-hidden; it is not a client-code contract.

`IModule<TModule>` exposes only `ValidateAsync(...)`. The generated defaulting, override-selection, value-expression, and interpolation phases are private async instance helpers invoked by that public orchestrator.

## Override Precedence

Raw string selection and typed resolution use the same override lookup order:

1. module `Name`;
2. module `Group`;
3. module ID;
4. environment override-resolution tags, in order.

The first matching override wins, including when its resolved value equals the module property's current value or is the same collection instance. Override presence is tracked independently from the resolved value and its terminal-evaluation state; a selected value does not fall through to an inherited environment's override merely because it compares equal. A present override whose `&{...}` or `*{...}` target is undefined fails resolution instead of falling through to a less specific override. Separating raw selection from evaluation does not change precedence or path construction.

## Examples

### Shell expression passed literally

```json
{
  "cyborg.modules.subprocess.v1": {
    "command": {
      "executable": "/bin/bash",
      "arguments": ["-c", "echo ${#HOME}"]
    }
  }
}
```

After generated interpolation, the worker receives:

```text
echo ${HOME}
```

### Mixed interpolation and literal expression

Given `prefix = "resolved"`:

```text
${prefix}/${#HOME} -> resolved/${HOME}
```

### Typed port override

```text
host.port = 22
@my_module.liveness_probe_port = "&{host.port}"
```

Generated typed resolution reads the current integer. `"${host.port}"` is text and does not satisfy an `int` property. `"*{host.port}"` instead stores `22` at the moment the override variable is defined, so a later change to `host.port` does not affect it. `*{@host.port}` is invalid; entry-point selection is expressed with `&{@host.port}`.

### Deferred override

For a property marked `[IgnoreInterpolation]`:

```text
stored override: ${assertion.result}
generated preparation result: ${assertion.result}
worker interpolation after assertion execution: current assertion result
```

The override is selected without evaluation, so it is not bound to stale environment state during validation.
