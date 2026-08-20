# Tagged String Production-Hardening Review

## Scope

This note reviews the tagged-string change set as production code rather than as an evaluation artifact. The core value-flow design is sound: `TaggedString` is a first-class runtime value, environment resolution preserves tags, interpolation unions tags, dynamic values can create tagged values, and generated module preparation accepts `TaggedString` properties while retaining `string` compatibility.

The remaining work is concentrated at presentation and execution boundaries, plus a few source-generator integration details. The goal of the cleanup is to make taint propagation boring: tags remain attached until an explicit raw-value boundary, and every Cyborg-controlled presentation path uses the same DI-backed rendering policy.

## Findings

### Rendering policy is not consistently centralized

Debugger description writers use `ITaggedStringRenderer`, but generated validation errors interpolate `TaggedString` values directly. That falls back to `TaggedString.ToString()`, whose context-free renderer only knows the built-in secret tag. Validation already carries `ModuleValidationContext.ServiceProvider`, so this path should use the canonical DI renderer instead of a reduced fallback policy.

The context-free `ToString()` fallback is still useful as a defensive last resort. It should not be the rendering mechanism used when a service provider is available.

### Tag handlers can currently recover a redacted raw value

`ITaggedStringTagHandler.Render(...)` receives the complete `TaggedString` as well as the current rendered text. A handler running after the secret handler can therefore read `TaggedString.Value` and undo redaction. Rendering should compose transformations over the current display value only; handlers do not need raw-value access to implement tag-specific presentation.

### Subprocess dispatch discards tags before diagnostic logging

`SubprocessModule` correctly carries arguments as `TaggedString`, but its worker converts them to raw `string` before constructing `ProcessStartInfo`. `DefaultChildProcessDispatcher` then logs that raw argument list. The dispatcher is the correct raw execution boundary, so tagged arguments should remain tagged until the dispatcher renders them for diagnostics and separately materializes their raw values into `ProcessStartInfo`.

This is broader than the generic subprocess module: Borg and network modules also dispatch child processes. A single dispatcher request model should own this boundary so callers cannot accidentally create a second logging representation that has already lost metadata.

### CLI startup logging mirrors raw command-line secrets

The CLI logs `Environment.GetCommandLineArgs()` verbatim. Typed `cyborg.types.secret.v1` values can therefore be written to console/file logs before the dynamic-value system has a chance to attach metadata. Command-line values are process-global raw strings and cannot be retroactively taint-tracked. The startup log should not mirror the raw command line; useful non-sensitive startup context can be logged structurally instead.

Invalid typed environment/config definitions also need care because the definition itself can contain a secret. Error reporting should identify the key/type and parsing problem without echoing the supplied value.

### `[Secret]` is coupled to interpolation

`SecretAspect` injects the secret tag by rewriting the interpolation expression. This unnecessarily ties a property invariant to an unrelated phase and breaks nullable `TaggedString` combined with `[IgnoreInterpolation]` because the generated expression calls `WithTag(...)` on `Nullable<TaggedString>`.

The invariant belongs in generated preparation independently of interpolation. The defaults stage already runs both before and after override selection, making it a natural place to enforce intrinsic property metadata, while final validation remains a useful assertion that generated preparation preserved the invariant.

### Generator integration bypasses compile-time contract discovery

`TypeSymbolHelpers` hard-codes metadata/global names for `TaggedString` and `WellKnownTags`. The validation generator already uses compile-time contract discovery for runtime types referenced from generated code. Tagged-string generator dependencies should participate in that same registration/discovery mechanism so type identity and generated expressions remain owned by the runtime assembly rather than duplicated as string literals in the analyzer.

### `TaggedString` owns presentation policy details that belong to renderers

`TaggedString.RedactedDisplay` is only meaningful to the secret rendering policy. The value type should own value/tag semantics, not a presentation constant for one well-known tag. The constant belongs with the secret renderer/handler.

`TaggedString.Equals(object)` also treats a raw `string` as equal even though `string.Equals(object)` cannot reciprocate and the hash codes differ. Cross-type convenience operators can compare textual values, but object equality should remain type-consistent.

### Migration diagnostics should remain narrow

`[Untagged]` is useful for structural strings that deliberately cannot participate in tagged-value flow. It should not become a blanket mechanism for silencing the migration diagnostic on arbitrary user-provided textual data. Existing annotations should be retained where the value is genuinely structural; execution-facing values that may reasonably carry sensitive data should prefer `TaggedString`.

## Target architecture

- `TaggedString` is an immutable value plus an opaque set of tags.
- Environment resolution, indirection, interpolation, overrides, decomposition/publication, and generated preparation preserve tags without interpreting them.
- Raw text is materialized only at explicit execution boundaries such as process invocation or compatibility retrieval as `string`.
- `ITaggedStringRenderer` is the canonical safe presentation boundary whenever DI is available. Tag handlers transform the current display value and cannot recover previously hidden raw text.
- `TaggedString.ToString()` remains a conservative context-free fallback, not the normal Cyborg rendering path.
- Intrinsic property metadata such as `[Secret]` is applied during generated preparation independently of interpolation and asserted during final validation.
- Source-generated references to tagged-string runtime contracts use the existing contract-discovery infrastructure.
- CLI and process diagnostics do not log raw representations that have already crossed out of the tagged-value model.

## Validation plan

Add focused regression coverage for:

- DI-backed custom-tag rendering in generated validation errors;
- secret redaction remaining effective when another tag handler also applies;
- nullable `[Secret]` + `[IgnoreInterpolation]` generated preparation;
- secret-tagged subprocess arguments reaching the process as raw values while logs contain only the rendered form;
- CLI startup/error logging not echoing typed secret argument values;
- object equality/hash behavior for `TaggedString`;
- compile-time contract discovery for the tagged-string types used by generated validation code.

The full solution should be restored, built in Release, and tested after the refactor. The current execution environment does not provide the `dotnet` CLI, so build/test validation must be reported separately unless a usable SDK becomes available during the cleanup.
