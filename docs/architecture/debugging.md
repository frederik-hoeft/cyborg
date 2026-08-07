# Workflow Debugging and Module Descriptions

This document describes the workflow debugging subsystem and the module-description pipeline used by debugger inspection. Both areas are still under active development; the contracts documented here describe the current implementation rather than a compatibility promise for the debugging/description APIs.

For the surrounding execution model, see [Architecture Overview](architecture-overview.md). For the generator that emits module descriptors, see [Source Generators](source-generators.md).

## Overview

Cyborg debugging operates at module execution boundaries. When a breakpoint matches, the module has already passed defaults, override resolution, interpolation, and validation, but its worker has not executed yet. A frontend can therefore inspect the final validated configuration, manage breakpoints, step, continue, or cancel before module side effects begin.

The subsystem is split into three layers:

- `Cyborg.Core` owns breakpoint matching, pause state, descriptor contracts, immutable description trees, serializer discovery, and serialization orchestration.
- `Cyborg.Cli.Debugging` owns the console frontend and its ConsoleAppFramework (CAF) command surface.
- `Cyborg.Cli` owns the main process command surface and composes the core/debugging services.

`IDebugFrontend` is the runtime adapter boundary, so another host can provide a non-console frontend without changing the execution engine. `IModuleDescriptionSerializer` is the serialization extension boundary, so applications can register additional description formats through DI without changing generated modules.

## Execution Boundary

The debugger hook is in `ModuleWorker<TModule>` after the configuration pipeline and before the worker's `ExecuteAsync` implementation:

```text
Load -> ApplyDefaults -> ResolveOverrides -> Interpolate -> Validate -> [DEBUG HOOK] -> ExecuteAsync -> Exit
```

At the hook:

1. Resolve the optional `IWorkflowDebugger`.
2. Return immediately when the debugger is absent or disabled.
3. Evaluate the breakpoint registry for the current module.
4. If a breakpoint matches, resolve the configured `IDebugFrontend` and call it.
5. Resume execution for `DebugResumeAction.Continue`, or return the cancellation path for `DebugResumeAction.Cancel`.

The frontend is selected through `IDefault<IDebugFrontend>` and the `cyborg.core.debug:frontend` selection key. When the key is absent, `DebugOptions.Default.Frontend` supplies the default (`console`). If a breakpoint is hit but the selected frontend is unavailable, debugging fails explicitly instead of silently consuming the breakpoint.

## Breakpoints

`IBreakpointRegistry` stores numbered `BreakpointExpression` entries. Expressions are culture-invariant regular expressions with a match timeout and are evaluated against module id, module name when present, and module group when present.

| Expression | Meaning |
|---|---|
| `step-two` | Substring match against id/name/group |
| `^step-two$` | Exact name/group match |
| `cyborg\.modules\.empty\.v1` | Match the empty module id |
| `.*` | Match every module; used for stepping |

The `step` command registers a one-shot `.*` breakpoint and resumes. One-shot consumption is atomic: only the caller that successfully removes the matching one-shot breakpoint reports the match. Persistent breakpoints are unaffected.

Breakpoint state is session-wide:

| Action | Registry effect |
|---|---|
| `break at <expression>` | Add persistent expression |
| `break rm <id>` | Remove one expression |
| `break ls` | List current expressions |
| `step` | Add one-shot `.*` |
| `continue` | No change |
| `detach` | Clear all expressions |
| REPL EOF | Detach and continue |

## Frontend Boundary

The runtime-facing frontend contract is deliberately small:

```csharp
public interface IDebugFrontend : IKeyedService
{
    ValueTask<DebugResumeAction> PauseAsync(IDebugPauseContext context, CancellationToken cancellationToken);
}
```

`IDebugPauseContext` exposes only the state and debugger operations valid during a pause:

| Member | Purpose |
|---|---|
| `Module` / `ModuleId` | Current module and canonical id |
| `ModuleIdentity` | Compact id/name/group string |
| `Runtime` | Ambient module runtime for future debugger features |
| `Breakpoints` | Session breakpoint registry |
| `RequestStep()` | Register the one-shot step breakpoint |
| `Detach()` | Clear breakpoint state |

The pause context intentionally does not expose `IServiceProvider` and does not own description serialization. Frontends receive their own dependencies through DI, keeping the pause model free of service-locator behavior.

## Console REPL and CAF Isolation

`ConsoleDebugFrontend` owns the interactive lifecycle: print the breakpoint banner, read one line through `IDebugReplIo`, dispatch it, and repeat until a command returns a resume action. EOF is treated as detach + continue.

CAF owns command routing, aliases, typed argument binding, validation/error output, and generated help. The only parser Cyborg retains is `CommandLineTokenizer`, because a REPL receives one command-line string while CAF consumes an argument vector. The tokenizer is lexical only: it handles whitespace, single/double quotes, empty quoted arguments, and escaping, but knows nothing about debugger command grammar.

CAF v5 generates its command router from registration call sites in the consuming compilation. The main CLI and debug REPL therefore deliberately live in different compilations:

```text
Cyborg.Cli
  Program / Commands                 -> main CAF router (`run`, ...)

Cyborg.Cli.Debugging
  DebugCommandDispatcher             -> debug CAF router (`continue`, `break`, ...)
```

This prevents debug `help` or command routing from exposing the global CLI command set, and prevents the REPL from recursively invoking the main process command surface. `Cyborg.Cli` references and imports `ICyborgCliDebugServices`, but the debug assembly owns its CAF registrations and generated router.

Breakpoint expressions may consume multiple positional tokens, so `break at backup group` is interpreted as the expression `backup group`. Quoting remains available when whitespace grouping must be explicit, for example `break at "backup  group"`.

### Built-in commands

| Command | Aliases | Behavior |
|---|---|---|
| `continue` | `c`, `resume` | Resume until the next breakpoint |
| `step` | `s` | Add one-shot `.*` and resume |
| `detach` | none | Clear breakpoints and resume |
| `inspect` | `i` | Serialize and print the current module when it implements `IModuleDescriptor` |
| `break at <expression>` | `b at ...` | Add a persistent breakpoint |
| `break ls` | `break list`, `b ls`, `b list` | List breakpoints |
| `break rm <id>` | `break remove`, `b rm`, `b remove` | Remove one breakpoint |
| `cancel` | `q`, `quit` | Return the workflow cancellation action |
| `help [command]` | `h`, `?` | Translate to CAF's generated help |

The old `IDebugReplCommand` abstraction no longer exists. Adding a console-debugger command means registering another CAF command in `DebugCommandDispatcher`; that CLI-specific API remains internal.

## Module Identity and Descriptor Capability

`IModule` remains the stable runtime module contract and does not depend on debugging or description infrastructure. Descriptor support is an optional capability expressed separately by `IModuleDescriptor`.

Generated validation targets implement `IModuleDescriptor` directly and override `ToString()` through `ModuleIdentity.Format(ModuleId, Name, Group)`. The console `inspect` command checks whether the current `IModule` also implements `IModuleDescriptor`; hand-written or otherwise non-generated modules remain valid `IModule` implementations and simply cannot be structurally inspected unless they opt into the descriptor contract.

## Module Description Pipeline

### Descriptor contract

`IModuleDescriptor` is the format-neutral producer contract:

```csharp
public interface IModuleDescriptor
{
    ValueTask DescribeAsync(IObjectDescriptionBuilder descriptionBuilder, CancellationToken cancellationToken);
}
```

`DescribeAsync` is asynchronous and cancellable because descriptor production may eventually require asynchronous work. Current generated implementations build the tree synchronously and return `ValueTask.CompletedTask`. Nested builder callbacks therefore remain synchronous.

### Tree construction and service ownership

`IModuleSerializationService` owns the mutable construction phase. `BuildAsync` creates the internal default builder/component factory, awaits `DescribeAsync`, and returns an immutable `IDescriptionObjectComponent`. `SerializeAsync` either accepts an explicit `IModuleDescriptionSerializer` or resolves one by format through `IModuleDescriptionSerializerRegistry`.

The public contracts needed by serializer authors are:

- `IModuleDescriptor`
- `IObjectDescriptionBuilder` / `ICollectionDescriptionBuilder` for descriptor production
- immutable `IDescription*Component` tree interfaces
- `IDescriptionComponentWriter`
- `IModuleDescriptionSerializer`
- `IModuleDescriptionSerializerRegistry`
- `IModuleSerializationService`
- `IModuleDescriptionServices` for DI composition

Concrete mutable builders, component records/factory, built-in serializers/writers, and the default serialization/registry implementations are internal. `IModuleDescriptionServices` exposes public Jab factory methods so consuming applications can import the service module without requiring those implementation constructors to be visible across assembly boundaries.

Description services are registered independently from `IDebugServices`. This allows non-debugging clients to use module descriptions and custom formats without importing breakpoint/debugger infrastructure. Applications can add more `IModuleDescriptionSerializer` registrations; the registry consumes all registered serializers and requires format keys to be unique case-insensitively.

`ModuleDescriptionFormats.Text` and `.Json` use MIME-style keys (`text/plain` and `application/json`). Convenience methods such as `ToTextAsync` and `ToJsonAsync` resolve those keys through the same registry rather than directly constructing built-in serializers.

### Hints

Values and properties carry `ImmutableArray<string>` hints. Hints are arbitrary string keys with no mandatory semantics in the core tree. `PropertyAspect.RegisterDescriptorHints` is a no-op extension hook by default; future attributes such as `[Secret]` can contribute keys, and serializers can opt into the keys they understand. Unknown hints remain preserved.

### Source-generated traversal

`InspectionSectionRenderer` emits `DescribeAsync` from the same `PropertyModel` graph used by validation, interpolation, and defaults. It recursively describes `[Validatable]` records and supported collection element records without runtime reflection.

Collection classification and enumeration semantics are shared with the rest of the validation generator: `string` is scalar despite implementing `IEnumerable<char>`, nullable references/value collections are guarded before enumeration, and default `ImmutableArray<T>` values are never enumerated.

Generator accessibility checks use a `VisibilityContext` anchored to the root generated module symbol. This models the lexical location of generated helper methods correctly even when recursive processing reaches properties declared on nested validatable types or inherited base types; the property's value type is not an accessibility context.

## DI Composition

Core registration is split by responsibility:

```text
ICyborgCoreServices
  imports IModuleDescriptionServices
  imports IDebugServices

IModuleDescriptionServices
  built-in serializers
  serializer registry
  serialization service

IDebugServices
  debug options provider
  breakpoint registry
  workflow debugger
  frontend selection/default service

ICyborgCliDebugServices (Cyborg.Cli.Debugging)
  console IDebugFrontend factory
```

Jab generates consuming providers in the project that imports a service module. When a registered implementation type would otherwise need cross-assembly constructor visibility, the owning module exposes a public static factory method and keeps the concrete implementation internal.

## Testing Expectations

Description tests should consume `IModuleDescriptionServices` through a small Jab test provider so the same registration boundary used by applications is exercised. Generator-backed tests must cover scalar strings, nested objects/collections, nullable shapes, default/empty `ImmutableArray<T>`, hints, custom serializers, and cancellation propagation.

Console debugger tests should execute the real `Cyborg.Cli.Debugging` dispatcher and verify aliases, nested commands, quoted/unquoted expressions, repeated pauses, EOF, inspection, and generated help. A regression assertion must ensure debug help does not contain the main CLI `run` command; this protects the separate-CAF-compilation invariant.
