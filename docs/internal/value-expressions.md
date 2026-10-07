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

`Interpolate`, generated string preparation, and key interpolation are textual. They reject `&{...}` and `*{...}`, including when that form is the entire string. String and `TaggedString` module properties therefore keep using `${...}`. Non-string properties, including collections, resolve stored `&{...}` and already-captured snapshots through typed override resolution.

`[IgnoreInterpolation]` still skips the generated textual pass. A `${...}` template selected from an override or a default stays raw for the worker. A captured textual override contributes its snapshot, not the `*{...}` expression, because the expression was consumed at definition. `[Secret]` and other tag unions are unchanged: interpolation unions tags across operands, and indirection unions tags from a `TaggedString` wrapper onto a textual target.

Argument binding resolves a required argument and then stores that resolved value. If the resolved text would be parsed again as indirection, capture, or illegal embedded syntax, the copy is stored as a terminal snapshot so finalizing an escape cannot turn the copy into a live reference.

## Mechanism

The grammar constants and source-generated matchers live next to the existing variable syntax. A small parser classifies a string as text, lazy indirection, or eager capture, and throws for embedded or malformed active references. `EnvironmentLike` keeps scope walk, cycle detection, tag union, and type conversion. It asks the parser what a string is instead of inferring indirection from an exact `${...}` match.

Snapshots are stored as an internal wrapper around the resolved object. Resolution unwraps the wrapper and marks the result terminal so the public read does not interpolate or finalize it. Enumeration unwraps the wrapper and returns the snapshot value.

Textual passes still finalize one escape layer on the whole result. A terminal string spliced into a larger interpolation, or selected as a raw string that the generated interpolation pass will visit, is shielded by inserting one `#` after each operator brace. That single pass restores the snapshot, including characters that look like expressions. Properties marked `[IgnoreInterpolation]` select the unshielded snapshot because no generated pass will restore a shield. The generator passes that choice into raw string selection and collection override resolution. Key interpolation never sets it, because keys are not values and cannot carry the typed forms.

Typed override resolution reports whether its result is terminal. `Resolve` does not run a second textual pass over a terminal string. Non-terminal strings keep the existing behavior: internal resolution interpolates references, and the public boundary finalizes one escape layer.

## Compatibility

This is a breaking change for values that used exact `${...}` as typed indirection. Those values become `&{...}`, with `@` preserved when the reference was entry-point-relative (`${@host.port}` becomes `&{@host.port}`). Exact `${...}` is now always text, so an exact `${port}` whose target is an `int` stringifies instead of yielding the integer. Composite templates, `${@}` and `${@@}`, hash escapes, unresolved placeholders, override precedence, and `[IgnoreInterpolation]` of textual templates are unchanged.

Samples that inject module contexts, remote-shell objects, and integer ports through overrides use `&{...}`. Passphrases and other textual properties stay on `${...}`.
