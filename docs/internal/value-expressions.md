# Value Expressions

This note is the design for splitting environment values into string interpolation, lazy typed indirection, and eager typed capture. The steady-state contract lives in [Interpolation and Override Resolution](../architecture/interpolation.md) and in the variable-resolution section of [System Architecture](../architecture/architecture-overview.md).

## Problem

`"..."${foo}"..."` is one spelling for two operations. An exact `${foo}` becomes typed indirection when `foo` is defined and the requested type accepts the target; the same spelling is string interpolation when the string has surrounding text, when the target is missing, or when the caller asks for text. `${@foo}`, `${@}`, and `${@@}` share that overload. Readers cannot tell which operation a stored string will perform without the resolution site, the requested type, and whether the target currently exists.

Override selection depends on that overload. Non-string module properties resolve exact `${...}` references, while string properties deliberately do not, so `[IgnoreInterpolation]` can keep a raw template. The two paths disagree about what an exact expression means.

## Forms

Value grammar and key grammar are separate. Keys, including override addresses such as `@${my_module}.property`, stay interpolation-only. The forms below apply to values.

| Form | Spelling | When it runs | Result |
|------|----------|--------------|--------|
| Interpolation | `${expr}` inside text, including an exact `${expr}` | At each read or `Interpolate` call | Text. Missing targets stay as the original placeholder. |
| Lazy indirection | `&{expr}` as the entire value | At each read | The target's current type and value. |
| Eager capture | `*{identifier}` as the entire value | Once, in `SetVariable`, when the variable is defined | A snapshot of the target's type and value. |

`expr` for interpolation and indirection is an identifier, `@identifier`, `@`, or `@@`. `@identifier` resolves from the original resolution entry point. `@` and `@@` are the current and entry-point namespaces. They remain ordinary interpolation when written as `${@}` and `${@@}`. Indirection accepts the same expressions, so `&{@}` and `&{@@}` are typed namespace references and `&{@identifier}` is entry-point indirection.

Capture accepts an identifier only. `*{@identifier}`, `*{@}`, and `*{@@}` are reserved and rejected. Capture has no entry-point form.

An unescaped `&{...}` or `*{...}` with any leading or trailing text is a syntax error (`FormatException`). A leading `#` escapes one evaluation pass for every operator: `${#name}` becomes the literal `${name}`, `&{#name}` becomes `&{name}`, and `*{#name}` becomes `*{name}`. Extra hashes peel one per pass, and the text revealed by removing the hash is not evaluated in that same pass. Escapes are how a value holds the characters of another form.

Undefined targets in `&{...}` and `*{...}` fail resolution with `InvalidOperationException`. They do not fall through to a placeholder and they do not skip an override that is present but broken. Interpolation of a missing `${...}` is unchanged: the placeholder remains. Cycles still throw `InvalidOperationException`. A resolved value whose CLR type is not the requested type still throws `InvalidCastException`.

## Timing

`SetVariable` is the definition boundary for environment values.

- `*{identifier}` resolves the target immediately and stores the snapshot. The expression text is not stored. A missing target fails the write.
- `&{expr}` is stored unchanged and resolved on every read.
- Other text is stored unchanged, after rejecting embedded unescaped `&{...}` and `*{...}`. `${...}` inside that text stays lazy.

Capture copies the reference returned by resolving the target. It does not deep-clone. Later mutations of a reference-type target are visible through the snapshot; later replacement of the target variable is not. A captured string is terminal: reading it does not interpolate it again and does not strip another escape layer.

`Interpolate` and key interpolation remain textual and reject active `&{...}` and `*{...}` forms. Generated module preparation is split into a typed value-expression pass followed by the textual interpolation pass. The typed pass visits `string` and `TaggedString` values directly, including values in nested validatable records and supported collections, resolves whole-value `&{...}` / `*{...}` expressions, and leaves `${...}` templates untouched for the later interpolation pass. This applies equally to directly configured values and values selected from overrides.

`[IgnoreInterpolation]` skips only the generated textual pass. Typed value expressions still resolve, while a `${...}` template selected from an override or a default stays raw for the worker. `[Secret]` and other destination preparation invariants are re-applied after value-expression resolution, so replacing a property through indirection cannot declassify it. Source tags are preserved and interpolation continues to union tags across operands.

Argument binding resolves a required argument and then stores that resolved value. If the resolved text would be parsed again as indirection, capture, or illegal embedded syntax, the copy is stored as a terminal snapshot so finalizing an escape cannot turn the copy into a live reference.

## Mechanism

The grammar constants and source-generated matchers live next to the existing variable syntax. A small parser classifies a string as text, lazy indirection, or eager capture, and throws for embedded or malformed active references. `EnvironmentLike` keeps scope walk, cycle detection, tag union, and type conversion. It asks the parser what a string is instead of inferring indirection from an exact `${...}` match.

Snapshots are stored as an internal wrapper around the resolved object. Resolution unwraps the wrapper and marks the result terminal so the public read does not interpolate or finalize it. Enumeration unwraps the wrapper and returns the snapshot value.

Textual passes still finalize one escape layer on the whole result. Terminal text crossing into a later semantic phase is shielded by inserting one `#` after the operator brace that the later phase could otherwise interpret. Raw textual override selection therefore protects captured `&{...}` / `*{...}` text from the value-expression pass, and additionally protects `${...}` when generated interpolation will follow. The corresponding later pass removes that one layer without evaluating the freshly exposed syntax. Collection capture is shallow: capturing a collection does not recursively mark or shield its elements, which continue through the destination property's normal element preparation.

Typed override resolution reports whether its result is terminal. `Resolve` does not run a second textual pass over a terminal string. Non-terminal strings keep the existing behavior: internal resolution interpolates references, and the public boundary finalizes one escape layer. Required template arguments use the same shielding mechanism when a resolved textual value is rebound into another environment, avoiding a concrete environment implementation dependency while preserving the already-resolved value.

## Compatibility

This is a breaking change for values that used exact `${...}` as typed indirection. Those values become `&{...}`, with `@` preserved when the reference was entry-point-relative (`${@host.port}` becomes `&{@host.port}`). Exact `${...}` is now always text, so an exact `${port}` whose target is an `int` stringifies instead of yielding the integer. Composite templates, `${@}` and `${@@}`, hash escapes, unresolved placeholders, override precedence, and `[IgnoreInterpolation]` of textual templates are unchanged.

Samples that inject module contexts, remote-shell objects, and integer ports through overrides use `&{...}`. Textual module properties use `${...}` when they want textual interpolation and may use a whole-value `&{...}` / `*{...}` when they want typed reference/capture semantics before interpolation.
