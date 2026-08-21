# Interpolation and Override Resolution

This document defines the runtime contract for storing expressions, resolving variables, selecting module-property overrides, applying generated interpolation, and explicitly interpolating deferred values. It also defines the escape syntax for preserving literal `${...}` expressions.

The design separates **selection** from **evaluation**. Stored values remain late-bound, generated module preparation may select string overrides without evaluating them, and explicit resolution APIs remain complete materialization boundaries.

## Expression Syntax

Cyborg recognizes the following ordinary interpolation expressions:

| Syntax | Meaning |
|--------|---------|
| `${identifier}` | Resolve `identifier` relative to the scope where the expression is encountered. |
| `${@identifier}` | Resolve `identifier` relative to the original resolution entry point. |
| `${@}` | Resolve the current scope's namespace. |
| `${@@}` | Resolve the original entry point's namespace. |

An exact ordinary expression may resolve to a non-string value. This enables typed indirection such as an `int` module property overridden through `${host.port}`. Composite strings always remain strings.

Unresolved ordinary expressions remain unchanged. Cyclic references fail with `InvalidOperationException` rather than recursing indefinitely.

## Literal Escape Syntax

A `#` immediately after `${` marks a final-phase literal:

| Input | Result after one interpolation pass |
|-------|-------------------------------------|
| `${#HOME}` | `${HOME}` |
| `${##HOME}` | `${#HOME}` |
| `${###HOME}` | `${##HOME}` |

Each interpolation pass removes exactly one leading `#`. The expression exposed by that removal is **not rescanned during the same pass**. For example, even when an environment variable named `HOME` exists, interpolating `${#HOME}` produces the literal `${HOME}` rather than resolving it.

This syntax is intentionally outside the ordinary interpolation grammar. Lazy variable resolution therefore ignores escaped expressions until an explicit or generated interpolation boundary is reached.

## Runtime Phases

### 1. Storage

`SetVariable(...)` stores values unchanged. String expressions are not evaluated when defined.

This preserves:

- unresolved and forward references;
- references whose value changes after definition;
- entry-point-sensitive `${@...}` and `${@@}` behavior;
- inherited-scope lookup;
- typed exact-reference indirection;
- escaped literals.

### 2. Variable resolution

`TryResolveVariable(...)` evaluates a variable from the caller's entry point. Ordinary references are resolved recursively and composite strings are interpolated. String results then remove one escape layer.

Resolution remains late-bound: the referenced value and applicable scope are determined when the variable is read, not when it was stored.

### 3. Module-property override selection

Generated preparation treats string and non-string properties differently:

- **String properties:** the generated validation support context selects the first matching stored override without evaluating its contents.
- **Non-string properties:** the context performs full typed resolution, including exact-reference indirection.

Raw string selection is required so `[IgnoreInterpolation]` applies to the effective value regardless of whether it came from JSON, a default, or an override. It also prevents override lookup from performing an accidental interpolation pass before generated interpolation.

These operations are not part of the normal worker-facing environment API. Source-generated preparation code accesses them through `ModuleValidationContext` in the `Cyborg.Core.Modules.Validation.Internal` namespace. This IntelliSense-hidden CLR bridge carries the runtime and service provider required by the generated phases, while the corresponding environment operations remain internal interface members.

Typed override resolution is therefore a generated-pipeline concern rather than a client-code API. Module workers use the ordinary environment operations described under [API Boundaries](#api-boundaries).

### 4. Generated interpolation

The generated validation pipeline performs:

1. apply defaults;
2. select or resolve overrides;
3. reapply defaults;
4. interpolate eligible strings through the generated validation context;
5. validate constraints.

The generated interpolation operation first resolves ordinary expressions and then removes one escape layer. It is applied recursively to eligible string properties in nested `[Validatable]` records and supported collections.

Properties marked `[IgnoreInterpolation]` skip this phase. Their effective value remains unchanged for worker-controlled interpolation, including values supplied through defaults or overrides.

### 5. Explicit and deferred interpolation

Module workers and other handwritten consumers use one interpolation API:

```csharp
TaggedString result = runtime.Environment.Interpolate(value);
string raw = result.Value; // execution-facing raw string
```

`Interpolate(...)` and `TryResolveVariable(...)` are complete evaluation boundaries. They resolve ordinary expressions recursively and remove one escape layer in string results. Interpolation returns a `TaggedString` whose tags are the union of the template's tags and the tags of every successfully resolved interpolation operand. Retrieving a tagged result as `string` still yields the raw value for compatibility, but discards tags; prefer `TryResolveVariable(..., out TaggedString)`.

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
- `SetVariable(...)` and `TryRemoveVariable(...)` for environment state.

Generated preparation additionally requires raw string override selection, typed scalar and collection override materialization, and access to the runtime and service provider shared by every preparation phase. These operations are grouped on `ModuleValidationContext` rather than exposed as public members of `IRuntimeEnvironment`. The context must be public because generated code is compiled into consuming assemblies, but it has a private constructor, lives in an `Internal` namespace, and is marked as editor-hidden; it is not a client-code contract.

`IModule<TModule>` exposes only `ValidateAsync(...)`. The generated defaulting, override-resolution, and interpolation phases are private async instance helpers invoked by that public orchestrator.

## Override Precedence

Raw string selection and typed resolution use the same override lookup order:

1. module `Name`;
2. module `Group`;
3. module ID;
4. environment override-resolution tags, in order.

The first matching override wins. Separating raw selection from evaluation does not change precedence or path construction.

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

### Deferred override

For a property marked `[IgnoreInterpolation]`:

```text
stored override: ${assertion.result}
generated preparation result: ${assertion.result}
worker interpolation after assertion execution: current assertion result
```

The override is selected without evaluation, so it is not bound to stale environment state during validation.
