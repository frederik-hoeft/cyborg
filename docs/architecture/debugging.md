# Workflow Debugging and Module Descriptions

This document describes the workflow debugging subsystem and the format-neutral module-description pipeline used by debugger inspection and other presentation clients.

For the surrounding execution model and lifecycle-hook pipeline, see [Architecture Overview](architecture-overview.md). For the source generation that produces rich module descriptors, see [Source Generators](source-generators.md).

## Overview

Cyborg debugging operates at module execution boundaries. A breakpoint is evaluated after defaults, override resolution, interpolation, and constraint evaluation have produced an `IValidationResult<TModule>`, but before that result is enforced and before the worker executes. The debugger therefore sees the prepared module together with any validation errors, including configurations that would fail normal execution.

The subsystem is split into three architectural layers:

- `Cyborg.Core` owns breakpoint state and matching, the runtime debugger contract, module descriptors, immutable description trees, serializer discovery, and serialization orchestration.
- `Cyborg.Cli.Debugging` provides the console frontend and its isolated command surface.
- `Cyborg.Cli` is the host composition root: it registers the CLI debugger services and selects runtime configuration sources.

`IDebugFrontend` is the host-facing presentation boundary. Hosts can provide a console, remote, graphical, or other frontend without changing module execution. Module descriptions are independent of debugging: `IModuleDescriptionSerializer` is the format extension boundary, so applications can register additional output formats through DI and reuse the same descriptor tree elsewhere.

## Execution Boundary

Debugging participates in execution through the general pre-execution hook pipeline rather than being hard-wired into `ModuleWorker<TModule>`:

```text
Load
  -> Apply Defaults
  -> Resolve Overrides
  -> Reapply Defaults
  -> Interpolate
  -> Evaluate Constraints
  -> OnValidationAsync
  -> Validation Hooks
  -> [PRE-EXECUTION HOOKS: DEBUGGER MAY PAUSE]
  -> EnsureValid
  -> ExecuteAsync
  -> Post-Execution Hooks
```

The validation result carried into the debugger always contains the prepared module. If the result is invalid, the frontend can inspect both that module and its errors before `EnsureValid()` would reject execution. Returning `Continue` resumes the normal lifecycle; returning `Cancel` lets the debugging hook produce a canceled module result through the normal result-building path without invoking the worker.

The debugger itself is inactive when no breakpoints are registered. On an active pre-execution boundary it evaluates the breakpoint registry against the module and, when a breakpoint matches, resolves the selected `IDebugFrontend` and presents an `IDebugPauseContext`.

Frontend selection uses the keyed-service selection setting `cyborg.core.debug:frontend`. Core deliberately has no implicit frontend (`DebugOptions.Default.Frontend` is `null`), because frontend policy belongs to the host. `Cyborg.Cli.Debugging` registers the built-in `console` frontend; CLI deployments must select `console` in host configuration before using `--break-at`. A host can instead register and select a different keyed frontend.

## Breakpoints

`IBreakpointRegistry` stores numbered regular-expression breakpoints. Expressions are culture-invariant and matched against the module ID plus `Name` and `Group` when present. Matching follows breakpoint ID order, giving the registry a stable session view even though registration/removal is thread-safe.

| Expression | Meaning |
|---|---|
| `step-two` | Substring match against ID/name/group |
| `^step-two$` | Exact name/group match |
| `cyborg\.modules\.empty\.v1` | Match the empty module ID |
| `.*` | Match every module; used for one-shot stepping |

Breakpoint state belongs to the workflow debugging session rather than to an individual pause:

| Action | Registry effect |
|---|---|
| `break at <expression>` | Add a persistent expression |
| `break rm <id>` | Remove one expression |
| `break ls` | List current expressions |
| `step` | Add a one-shot `.*` expression and resume |
| `continue` | Resume without changing the registry |
| `detach` | Clear all breakpoint state and resume |
| REPL EOF | Detach and continue |

One-shot removal is atomic: a matching one-shot breakpoint is consumed by the caller that successfully removes it. Persistent breakpoints remain registered until explicitly removed or detached.

## Frontend Boundary

The runtime-facing frontend contract is deliberately small:

```csharp
public interface IDebugFrontend : IKeyedService
{
    ValueTask<DebugResumeAction> PauseAsync(IDebugPauseContext context, CancellationToken cancellationToken);
}
```

`IDebugPauseContext` exposes the state and debugger operations that are valid during a pause:

| Member | Purpose |
|---|---|
| `ModuleId` | Canonical versioned ID of the paused module |
| `ValidationResult` | Prepared module, validity state, and validation errors |
| `Runtime` | Runtime associated with the paused execution boundary |
| `Services` | Host service provider associated with the executing module |
| `Breakpoints` | Session breakpoint registry |
| `RequestStep()` | Add the one-shot step breakpoint |
| `Detach()` | Clear session breakpoint state |

The pause context intentionally carries the module's service provider because frontend command dispatch may need ordinary host services alongside pause-local state. It is an integration boundary, not a general recommendation to use service-location within module code.

## Console REPL and CAF Isolation

`ConsoleDebugFrontend` owns the interactive pause lifecycle: display the breakpoint state, read a prompt-aware command line through `IDebugReplIo`, dispatch it, and continue until a command returns a resume action. EOF detaches and resumes. Inspection serializes the paused module descriptor and then reports any associated validation errors so failed configuration can be correlated with its prepared state.

The console frontend uses ConsoleAppFramework (CAF) for command routing, aliases, argument binding, validation, generated help, and command dependency injection. Cyborg retains only a lexical tokenizer because an interactive REPL receives one input string while CAF consumes an argument vector. Quoting and escaping are therefore handled before CAF dispatch, while command grammar remains CAF-owned.

The process CLI and debugger command surfaces live in separate compilations:

```text
Cyborg.Cli
  main CAF command surface (`run`, ...)

Cyborg.Cli.Debugging
  debugger CAF command surface (`continue`, `break`, ...)
```

This isolation prevents debugger help and routing from exposing or recursively invoking the process-level CLI commands. During one debugger command dispatch, pause-local objects are layered over the module's host service provider so command classes can receive both kinds of dependencies through constructor injection.

`IDebugReplIo` is the console presentation extension boundary. It owns prompt-aware reads and semantic writes classified by `OutputKind` (`Text`, `Status`, `Success`, `Warning`, and `Error`). The command layer therefore does not depend on a specific terminal rendering library; a richer I/O implementation can style those categories without changing debugger command behavior.

Breakpoint expressions may consume multiple positional tokens, so `break at backup group` is interpreted as the expression `backup group`. Quoting remains available when whitespace grouping must be explicit, for example `break at "backup  group"`.

### Built-in commands

| Command | Aliases | Behavior |
|---|---|---|
| `continue` | `c`, `resume` | Resume until another breakpoint matches |
| `step` | `s` | Register a one-shot `.*` breakpoint and resume |
| `detach` | none | Clear breakpoint state and resume |
| `inspect` | `i` | Serialize the prepared module descriptor and print associated validation errors |
| `break at <expression>` | `b at ...` | Add a persistent breakpoint |
| `break ls` | `break list`, `b ls`, `b list` | List breakpoints |
| `break rm <id>` | `break remove`, `b rm`, `b remove` | Remove one breakpoint |
| `cancel` | `q`, `quit` | Cancel the paused module execution |
| `help [command]` | `h`, `?` | Display CAF-generated debugger help |

Debugger commands are organized into focused internal command classes. `IDebugReplIo` remains public because console presentation is an intended extension boundary; command routing itself remains an implementation detail of the CLI frontend.

## Module Identity and Descriptor Capability

`IModule` defines the runtime identity and inspection surface shared by all modules: `Name`, `Group`, and `GetDescriptor()`. `IModuleDefinition` adds the static versioned `ModuleId` used for loading and execution. Short identity strings combine these values for diagnostics and breakpoint banners without relying on the concrete module type.

Descriptor support is therefore not an optional debugger-only capability. Generated module records return their rich generated descriptor from `GetDescriptor()`, while `ModuleBase` supplies a minimal fallback containing CLR type, name, and group for hand-written modules. Consumers such as `inspect` can always request a descriptor and do not need to special-case whether a module implements a separate capability interface.

## Module Description Pipeline

### Descriptor contract

`IModuleDescriptor` is the format-neutral producer contract:

```csharp
public interface IModuleDescriptor
{
    ValueTask DescribeAsync(IObjectDescriptionBuilder descriptionBuilder, CancellationToken cancellationToken);
}
```

Descriptor production is asynchronous and cancellable at the contract boundary. Generated descriptors populate the supplied builder directly; nested builder callbacks remain synchronous because tree construction itself does not require an asynchronous callback model.

### Tree construction and service ownership

`IModuleSerializationService` owns construction and serialization. It asks the descriptor to populate an object builder, materializes an immutable `IDescriptionObjectComponent` tree, and then delegates output to an `IModuleDescriptionSerializer`. Serializers can be supplied directly or resolved by format through `IModuleDescriptionSerializerRegistry`.

The public extension surface includes the descriptor/builder contracts, immutable description-component interfaces, the component writer abstraction, serializer and registry contracts, and `IModuleSerializationService`. Concrete mutable builders and built-in serializer implementations remain internal. This keeps tree construction controlled by the core service while allowing external serializer implementations to consume the stable immutable model.

Description services are registered independently from debugger services. Applications can therefore render module descriptions without enabling breakpoint infrastructure. Multiple `IModuleDescriptionSerializer` implementations may be registered; format keys are unique case-insensitively. Built-in text and JSON formats use `text/plain` and `application/json` and are resolved through the same registry as custom formats.

### Hints

Description properties and values may carry `ImmutableArray<string>` hints. Hints are arbitrary metadata keys with no mandatory semantics in the description tree. Generator aspects can contribute hints, the tree preserves them, and serializers decide which keys they understand. Unknown hints remain available to downstream consumers rather than being interpreted by the core model.

### Source-generated traversal

The module-validation generator emits rich descriptor traversal from the same property model used for validation, defaults, overrides, and interpolation. Nested `[Validatable]` records and supported collections are therefore described with the same structural classification used by the preparation pipeline, without runtime reflection.

The shared collection rules matter for descriptor correctness as well as validation: `string` remains a scalar despite implementing `IEnumerable<char>`; absent nullable collections are not enumerated; and a default `ImmutableArray<T>` remains distinct from an initialized empty array. Accessibility checks are evaluated relative to the lexical context of the generated partial module, including recursively reached nested or inherited properties.

## DI Composition

Core registration is separated by responsibility:

```text
ICyborgCoreServices
  imports IModuleDescriptionServices
  imports IDebugServices

IModuleDescriptionServices
  description tree construction
  built-in serializers
  serializer registry
  serialization service

IDebugServices
  debug options provider
  breakpoint registry
  workflow debugger
  frontend selection service
  pre-execution debugging hook

ICyborgCliDebugServices (Cyborg.Cli.Debugging)
  console REPL I/O
  keyed console debug frontend
```

This split keeps debugger mechanics, description serialization, and host presentation independently replaceable. The core defines contracts and orchestration; the CLI debugger assembly owns console-specific behavior; the application composition root decides which services and configuration sources are active.

## Testing Expectations

Tests should preserve architectural boundaries rather than merely individual command implementations. Description coverage should exercise generated scalar/nested/collection traversal, nullable and default collection shapes, hint preservation, custom serializer registration, and cancellation. Debugger coverage should exercise breakpoint lifecycle, invalid prepared-module inspection, frontend selection, repeated pauses, command aliases/tokenization, EOF/detach behavior, and cancellation.

The console debugger tests should dispatch through the real debugger CAF command surface. In particular, generated debugger help must remain isolated from the main CLI `run` command, and alternate `IDebugReplIo` implementations must be able to observe prompt and semantic output categories without command classes depending on console-specific rendering.
