# Interpolation and Override Resolution

This document defines the runtime contract for storing value expressions, resolving variables, selecting module-property overrides, applying generated interpolation, and explicitly interpolating deferred values. It also defines the escape syntax for preserving literal operator expressions.

The design separates **selection** from **evaluation**, and separates **text** from **typed values**. Stored text stays late-bound. An exact `&{...}` value stays a typed reference until it is read. An exact `*{...}` value is snapshotted when the variable is defined. Generated module preparation may select string overrides without evaluating them, and explicit resolution APIs remain complete materialization boundaries.

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

### 1. Storage

`SetVariable(...)` is the definition boundary.

- `*{identifier}` resolves the target immediately and stores a snapshot of its type and value. The expression text is not retained. The snapshot is the reference returned by that resolution; Cyborg does not deep-clone it. A missing target fails the write. `*{@...}` is rejected.
- `&{...}` is stored unchanged. It is an opaque reference.
- Other strings are stored unchanged after rejecting embedded unescaped `&{...}` and `*{...}`. `${...}` inside them is not evaluated yet.

This preserves forward references and entry-point-sensitive `${@...}` / `${@@}` behavior for interpolation, and keeps indirection late-bound.

### 2. Variable resolution

`TryResolveVariable(...)` evaluates a variable from the caller's entry point.

- A captured snapshot is returned as stored. It is not interpolated again, and a further escape layer is not removed.
- An exact `&{...}` reference is resolved recursively to the target's current value. Tags on a `TaggedString` wrapper are unioned onto a textual target.
- Other strings are interpolated. `${...}` references are resolved with the same scope rules. Missing interpolation targets stay in the result. String results then remove one escape layer.
- Non-string values are returned as stored.

An exact `${port}` whose target is an `int` therefore becomes the text `"22"` (or whatever `ToString` produces). The integer is available only through `&{port}` or through a capture of `port`.

### 3. Module-property override selection

Generated preparation separates override selection from value-expression evaluation:

- **Textual properties:** the generated validation support context selects the first matching stored override without evaluating its contents. This preserves late-bound `${...}` templates and, for `TaggedString`, any tags attached to the selected value. Captured textual snapshots are shielded only for the later phases that could otherwise reinterpret their contents.
- **Non-text properties:** the context performs full typed resolution. `&{...}` yields the current target and a capture yields the snapshot taken at definition. Collections use the collection-specific resolver before generated code materializes the declared collection shape. Capture of a collection is shallow; its elements are not recursively terminalized.

Raw textual selection is required so `[IgnoreInterpolation]` applies to the effective value regardless of whether it came from JSON, a default, or an override. It also keeps override precedence/selection independent from the semantics of the selected text.

These operations are not part of the normal worker-facing environment API. Source-generated preparation code accesses them through `ModuleValidationContext` in the `Cyborg.Core.Runtime.Services.Validation.Internal` namespace. This IntelliSense-hidden CLR bridge carries the runtime and service provider required by the generated phases, while the corresponding environment operations remain internal interface members.

Typed override resolution is therefore a generated-pipeline concern rather than a client-code API. Module workers use the ordinary environment operations described under [API Boundaries](#api-boundaries).

### 4. Generated value-expression preparation

After override selection, generated preparation recursively visits textual properties and textual elements in supported collections. A whole-value `&{...}` or `*{...}` is resolved as a typed value expression; `${...}` remains untouched for the later textual phase. The same pass applies to directly configured values and values supplied through overrides, so string-valued properties do not have a separate indirection model.

The pass is independent from `[IgnoreInterpolation]`: suppressing `${...}` interpolation does not suppress typed indirection or capture. Destination preparation invariants are applied again after this phase, so attributes such as `[Secret]` cannot be bypassed when a reference replaces the effective property value.

`ModuleBase.Name` and `ModuleBase.Group` opt out through `[IgnoreValueExpression]`. Their structural identity is consumed when the runtime binds the module environment before generated validation begins; rewriting those fields afterward would make the prepared module disagree with the namespace already selected for execution.

### 5. Generated interpolation

The generated validation pipeline performs:

1. apply defaults and preparation invariants;
2. select or resolve overrides;
3. resolve typed value expressions in textual properties;
4. reapply defaults and destination preparation invariants;
5. interpolate eligible strings through the generated validation context;
6. validate constraints.

The generated interpolation operation resolves ordinary `${...}` expressions and then removes one escape layer. It is applied recursively to eligible string properties in nested `[Validatable]` records and supported collections. It rejects active `&{...}` and `*{...}` because those belong to the preceding value-expression phase.

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

The first matching override wins. A present override whose `&{...}` or `*{...}` target is undefined fails resolution instead of falling through to a less specific override. Separating raw selection from evaluation does not change precedence or path construction.

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
