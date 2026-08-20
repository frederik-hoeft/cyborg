# Tagged String Production-Hardening Review

## Scope

This note records the production review of the first-class `TaggedString` implementation and the resulting hardening work. The intended steady state is deliberately simple: tagged textual values retain opaque metadata while they remain in Cyborg-managed data flow, and metadata is interpreted only when a value is rendered or deliberately lowered to a raw `string` at an execution/compatibility boundary.

The underlying runtime value-flow design is sound. `EnvironmentLike` stores tagged values without flattening them, interpolation and indirection preserve/union tags, dynamic values can construct tagged values, generated module preparation supports `TaggedString`, and legacy `string` retrieval remains available as an explicit metadata-loss compatibility path.

## Review findings and resolution

### Presentation policy was split across several mechanisms

The initial implementation used the DI-backed `ITaggedStringRenderer` for debugger serialization, but generated validation messages relied on `TaggedString.ToString()`. That made validation aware only of the built-in context-free secret fallback and bypassed application-defined tag handlers.

Generated validation diagnostics now render tagged values through `ModuleValidationContext`, which resolves the application `ITaggedStringRenderer`. `TaggedString.ToString()` remains only a context-free fallback for code that has no service-provider context.

### Renderer handlers could recover raw values after redaction

The initial tag-handler contract exposed both the raw `TaggedString` and the display text produced by earlier handlers. A later handler could therefore recover a raw value that an earlier handler had hidden.

Handlers now receive only the current display value. Rendering is deterministic over the tagged value's tag set, and a handler cannot recover raw text once another handler has removed it from the presentation pipeline. The renderer also rejects duplicate handlers for the same tag so tag ownership is unambiguous.

### Child-process logging crossed the raw-value boundary too early

`SubprocessModule` carried tagged arguments, but workers converted them to `ProcessStartInfo.ArgumentList` before dispatch. The default dispatcher then logged the already-untagged command line.

`ChildProcessInvocation` now carries tagged arguments and environment values to the dispatcher. The dispatcher renders arguments for diagnostics through `ITaggedStringRenderer` and separately materializes raw values immediately before process execution. The legacy `ProcessStartInfo` dispatch overload remains compatible, but deliberately omits argument logging because metadata is unavailable at that boundary.

This boundary is also used by Borg and the built-in network modules that launch child processes.

### CLI startup diagnostics mirrored raw arguments

The CLI previously logged the complete process command line before typed dynamic values were parsed. A secret supplied through `cyborg.types.secret.v1` could therefore be persisted to a log before it became a tagged value.

Startup logging no longer mirrors raw arguments. Invalid environment/configuration definitions are reported without echoing the supplied value. Configuration parsing diagnostics describe the structural/type error rather than logging the original definition. The configuration-argument APIs no longer return the full invalid definition either; retaining that raw value after deciding it must not be logged only creates another accidental disclosure path for future callers.

### `[Secret]` was coupled to interpolation

The initial implementation added the secret tag by rewriting the interpolation expression. Besides coupling unrelated concepts, this broke nullable `TaggedString` values when interpolation was explicitly disabled.

`[Secret]` is now a preparation invariant. The generated defaults/preparation stage applies intrinsic property tags both before and after override selection, so an override can replace the value but cannot declassify the destination property. Final validation independently asserts that the tag is present. `[IgnoreInterpolation]` therefore has no effect on secret-tag enforcement.

### Tagged-string generator types bypassed contract discovery

The source generator initially hard-coded metadata names for `TaggedString` and `WellKnownTags`. They now participate in the same compile-time contract registration/discovery mechanism as other runtime types referenced by generated code. Property analysis happens after those contracts are discovered, so type checks and generated expressions use the resolved symbols rather than duplicated names.

### Value and presentation responsibilities were mixed

`TaggedString` initially exposed a secret-specific redaction constant and object equality claimed cross-type equality with `string`. Secret presentation now lives with `SecretTagHandler`; `TaggedString` owns only raw value/tag semantics and conservative context-free formatting. Object equality is type-consistent while explicit textual comparison remains available through `IEquatable<string>` and the convenience operators.

### Migration diagnostics needed a deliberate compatibility boundary

`string` remains a supported module/property type. The validation generator emits `CYBORGVAL025` to encourage configurable textual properties to use `TaggedString`, while `[Untagged]` marks strings that are intentionally structural or otherwise outside tagged-value flow. Runtime retrieval as `string` remains supported and reports tagged-to-raw conversion through `ITaggedStringConversionObserver` when metadata is discarded.

### Preparation helper terminology still reflected defaults-only behavior

The shared generator helper used by both the defaults and override sections continued to be named around “default application” even after it became responsible for property-level preparation invariants. It is now named `PropertyPreparationRenderer`, with preparation-oriented method names, while the generated `ApplyDefaultsAsync` member remains stable. This keeps the implementation terminology aligned with the broader role of the pass without changing generated module contracts.

## Current architecture

- `TaggedString` is an immutable textual value plus an opaque set of string tags.
- Runtime storage, inheritance, interpolation, indirection, override selection, artifact publication, and generated preparation preserve tags without interpreting their meaning.
- String composition unions tags from every tagged operand.
- `cyborg.secret.v1` is the first globally interpreted tag; other tags are preserved even when no renderer is registered for them.
- `ITaggedStringRenderer` is the canonical safe presentation boundary whenever DI is available. Tag behavior is supplied by `ITaggedStringTagHandler` registrations.
- `TaggedString.ToString()` is a context-free fallback containing only globally built-in fallback policy. It is not the rendering mechanism for DI-aware Cyborg consumers.
- `[Secret]` imposes an intrinsic property tag during generated preparation and final validation asserts the invariant.
- `ChildProcessInvocation` retains tags until the child-process execution boundary. Raw subprocess values and compatibility retrieval as `string` are intentional metadata-loss operations.
- Description serializers, generated validation diagnostics, metrics labels, switch diagnostics, subprocess argument logging, and CLI target logging render tagged values through the DI policy.
- Raw CLI argument text is not mirrored into startup/error logs because it exists before tag metadata can be established.

## Boundaries and limitations

Tagged-string tracking is intentionally limited to textual values and Cyborg-managed/tag-aware operations. Once module code explicitly retrieves `.Value`, casts to `string`, or otherwise constructs an unrelated raw string, Cyborg cannot infer the original tags. Child-process stdout/stderr is likewise external output and cannot generally be associated with the tags of inputs that may have produced it.

Direct serialization through `TaggedStringJsonConverter` is a data/configuration operation, not a safe-display operation: tagged values serialize their raw value plus tags so configuration can round-trip. Presentation surfaces must use `ITaggedStringRenderer` instead.

## Verification

Focused regression coverage exercises tag union during interpolation/indirection, raw-string compatibility retrieval, dynamic tagged/secret values, `[Secret]` across overrides and nullable `[IgnoreInterpolation]`, DI-rendered validation messages, descriptor redaction, renderer composition, and value semantics.

A final Release build and complete test run are still required in an environment with the .NET 10 SDK. The current sandbox does not provide `dotnet`, `msbuild`, or `csc`, so this cleanup can only perform static call-site/source-generation review and repository-level diff validation here.
