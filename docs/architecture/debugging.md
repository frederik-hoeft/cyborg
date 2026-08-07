# Workflow Debugging and Module Descriptions

This document describes the workflow debugging subsystem and the module-description pipeline used by debugger inspection. Both areas are still under active development; the contracts documented here describe the current implementation rather than a compatibility promise for the debugging/description APIs.

For the surrounding execution model, see [Architecture Overview](architecture-overview.md). For the generator that emits module descriptors, see [Source Generators](source-generators.md).

## Overview

Cyborg debugging operates at module execution boundaries. When a breakpoint matches, the module has already passed defaults, override resolution, interpolation, and validation, but its worker has not executed yet. The frontend can therefore inspect the final validated configuration, manage breakpoints, step, continue, or cancel before module side effects begin.

The runtime and the user interface are intentionally separated:

- `Cyborg.Core` owns breakpoint matching, pause state, module-description construction, and serializer discovery.
- `Cyborg.Cli` owns console I/O and the interactive command surface.
- `IDebugFrontend` is the adapter boundary, so another host can provide a non-console frontend without changing the execution engine.

Module inspection is not a second hard-coded object walker. Generated modules implement the same format-neutral descriptor contract used by every serializer. The debugger selects the registered text serializer and asynchronously serializes that description tree.

## Execution Boundary

The debugger hook is in `ModuleWorker<TModule>` after the configuration pipeline and before the worker's `ExecuteAsync` implementation:

```text
Load -> ApplyDefaults -> ResolveOverrides -> Interpolate -> Validate -> [DEBUG HOOK] -> ExecuteAsync -> Exit
```

At the hook:

1. Resolve the optional `IWorkflowDebugger`.
2. Return immediately when the debugger is absent or disabled.
3. Evaluate the breakpoint registry for the current module.
4. If a breakpoint matches, call the configured `IDebugFrontend`.
5. Resume execution for `DebugResumeAction.Continue`, or return the cancellation path for `DebugResumeAction.Cancel`.

This keeps the non-debugging path to a cheap enabled check and prevents debugger inspection from observing partially processed module configuration.

## Breakpoints

`IBreakpointRegistry` stores numbered `BreakpointExpression` entries. Expressions are culture-invariant regular expressions with a match timeout and are evaluated against:

- module id,
- module name, when present, and
- module group, when present.

Examples:

| Expression | Meaning |
|---|---|
| `step-two` | Substring match against id/name/group |
| `^step-two$` | Exact name/group match |
| `cyborg\.modules\.empty\.v1` | Match the empty module id |
| `.*` | Match every module; used for stepping |

The `step` command registers a one-shot `.*` breakpoint and resumes. The next module consumes that one-shot breakpoint and pauses again. Persistent breakpoints are unaffected.

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
public interface IDebugFrontend
{
    ValueTask<DebugResumeAction> PauseAsync(
        IDebugPauseContext context,
        CancellationToken cancellationToken);
}
```

`IDebugPauseContext` exposes the current module and the debugger operations valid during a pause:

| Member | Purpose |
|---|---|
| `Module` / `ModuleId` | Current module and canonical id |
| `ModuleIdentity` | Compact id/name/group string |
| `Runtime` | Ambient module runtime for debugger features |
| `Breakpoints` | Session breakpoint registry |
| `InspectAsync(CancellationToken)` | Serialize the current module description using the debugger's text serializer |
| `RequestStep()` | Register the one-shot step breakpoint |
| `Detach()` | Clear breakpoint state |

Inspection is asynchronous all the way to the frontend. There is no synchronous `Inspect()` compatibility bridge and no sync-over-async `GetAwaiter().GetResult()` path.

## Console REPL

`ConsoleDebugFrontend` owns only the interactive lifecycle:

1. Print the breakpoint banner.
2. Read one line through `IDebugReplIo`.
3. Pass the line to `DebugCommandDispatcher`.
4. Repeat until a command returns a resume action.
5. Treat EOF as detach + continue.

`IDebugReplIo` keeps console access outside the core runtime and provides an asynchronous, cancellable input operation. `ConsoleDebugReplIo` is the production adapter and `TextDebugReplIo` supports tests/scripted input.

### Command parsing

ConsoleAppFramework (CAF) owns command routing, aliases, typed argument binding, validation/error output, and generated help. `DebugCommandDispatcher` configures one reusable CAF application and invokes it once per REPL command with service-provider disposal disabled between invocations.

The only parser Cyborg retains is `CommandLineTokenizer`, because a REPL receives a single command-line string while CAF consumes an argument vector. The tokenizer is intentionally lexical only. It handles whitespace, single/double quotes, empty quoted arguments, and escaping; it does not understand debugger command grammar.

CAF command names are normalized case-insensitively by the dispatcher before routing. Breakpoint expressions and other argument values are left untouched.

Breakpoint expressions may consume multiple positional tokens for compatibility with the original REPL, so `break at backup group` is interpreted as the expression `backup group`. Quote an expression when whitespace itself is significant or when shell-like grouping makes the intent clearer, for example:

```text
break at "backup  group"
```

### Built-in commands

| Command | Aliases | Behavior |
|---|---|---|
| `continue` | `c`, `resume` | Resume until the next breakpoint |
| `step` | `s` | Add one-shot `.*` and resume |
| `detach` | none | Clear breakpoints and resume |
| `inspect` | `i` | Asynchronously serialize and print current module state |
| `break at <expression>` | `b at ...` | Add a persistent breakpoint |
| `break ls` | `break list`, `b ls`, `b list` | List breakpoints |
| `break rm <id>` | `break remove`, `b rm`, `b remove` | Remove one breakpoint |
| `cancel` | `q`, `quit` | Return the workflow cancellation action |
| `help [command]` | `h`, `?` | Translate to CAF's generated help |

The old `IDebugReplCommand` handler abstraction no longer exists. CAF already provides the command-registration abstraction, generated syntax/help, and typed conversion, so maintaining a second command framework only duplicates responsibilities. Adding a CLI debugger command currently means registering another CAF command in `DebugCommandDispatcher`; this CLI-specific API is intentionally internal while the debugging frontend is WIP.

## Module Identity

Generated modules override `ToString()` through `ModuleIdentity.Format(ModuleId, Name, Group)`. This is the compact identity used by breakpoint banners and fallback output for modules that do not implement the descriptor contract.

`ModuleIdentity` is a validation-generator contract rather than a hard-coded generated type name. This keeps generated references aligned with the contract-discovery mechanism used elsewhere by the AOT generator.

## Module Description Pipeline

### Descriptor contract

`IModuleDescriptor` is the format-neutral producer contract:

```csharp
public interface IModuleDescriptor
{
    ValueTask DescribeAsync(
        IObjectDescriptionBuilder descriptionBuilder,
        CancellationToken cancellationToken);
}
```

`DescribeAsync` is asynchronous and cancellable because descriptor production may eventually require asynchronous work. The current generated implementations build their tree synchronously and return `ValueTask.CompletedTask`. Nested tree construction callbacks (`AddObject`, `AddCollection`, and their item variants) therefore remain synchronous and allocation-light.

### Tree construction

`ModuleDescription.BuildAsync` creates an internal builder/factory, awaits `DescribeAsync`, and returns an immutable tree rooted at `IDescriptionObjectComponent`.

The public tree contracts expose only what an external serializer needs:

- `IDescriptionObjectComponent`
- `IDescriptionCollectionComponent`
- `IDescriptionPropertyComponent`
- `IDescriptionValueComponent`
- `IDescriptionComponentWriter`

Concrete builders, component records, the component factory, and built-in component writers are implementation details and remain internal.

Values and properties carry an `ImmutableArray<string>` of **hints**. Hints are intentionally arbitrary string keys with no mandatory meaning in the core tree. Validation/property aspects can register hints while source is generated. A serializer may opt into any hints it understands, for example a future `secret` hint could cause a text/JSON serializer to redact a value. Unknown hints are preserved and ignored by serializers that do not recognize them.

### Source-generated traversal

`InspectionSectionRenderer` emits `DescribeAsync` from the same `PropertyModel` graph used by validation, interpolation, and defaults. It recursively describes `[Validatable]` records and supported collection element records without runtime reflection.

Collection classification and enumeration semantics are shared with the rest of the validation generator:

- `string` is explicitly scalar even though it implements `IEnumerable<char>`.
- nullable references are enumerated only when present.
- nullable value-type collections are unwrapped only when present.
- default `ImmutableArray<T>` values are not enumerated and are not silently converted to empty collections.

This avoids a separate inspection walker drifting away from generator/runtime semantics.

### Serialization

`IModuleDescriptionSerializer` is the public extension point for output formats:

```csharp
public interface IModuleDescriptionSerializer
{
    string Format { get; }

    ValueTask<string> SerializeAsync(
        IDescriptionObjectComponent description,
        CancellationToken cancellationToken);
}
```

Built-in implementations provide `text` and `json`. Their concrete serializer and writer classes are internal; callers normally use `ModuleDescription.ToTextAsync` / `ToJsonAsync` or resolve a serializer by format.

`IModuleDescriptionSerializerRegistry` is public and DI-backed. `DefaultModuleDescriptionSerializerRegistry` is internal and collects every registered `IModuleDescriptionSerializer` into a case-insensitive format map. Duplicate format keys fail during registry construction rather than choosing an implementation arbitrarily.

This is the intended client extension model: another project, such as a Borg integration, can implement a serializer for YAML or another representation and register that implementation in its DI composition without depending on Cyborg's internal tree builders or built-in writers.

`ModuleDescriptionFormats` contains the built-in `TEXT` and `JSON` format keys.

### Writer dispatch

Each immutable value node owns its concrete dispatch into `IDescriptionComponentWriter`:

- atom -> `WriteAtomAsync<T>`
- object -> `WriteAsync(IDescriptionObjectComponent, ...)`
- collection -> `WriteAsync(IDescriptionCollectionComponent, ...)`
- property -> `WriteAsync(IDescriptionPropertyComponent, ...)`

There is deliberately no catch-all `WriteAsync(IDescriptionValueComponent)` visitor overload. Such an overload cannot safely redispatch an arbitrary value component and previously allowed implementations to recurse back through `AcceptAsync` indefinitely.

## Debugger Inspection Integration

`WorkflowDebugger` resolves `IModuleDescriptionSerializerRegistry` once and requires the built-in text serializer. A `DebugPauseContext` captures that serializer for the active pause. `InspectAsync` then:

1. checks whether the module implements `IModuleDescriptor`,
2. calls `ModuleDescription.SerializeAsync` with the selected text serializer and pause cancellation token, or
3. falls back to `ModuleIdentity` when no descriptor is available.

The removed `ModuleInspection` helper is not part of this path. Module references, nested validatable records, and collections are represented by generated descriptor traversal instead of hard-coded runtime type switches.

## Service Registration

Core DI registers:

- `IBreakpointRegistry -> BreakpointRegistry`
- `IWorkflowDebugger -> WorkflowDebugger`
- built-in text and JSON `IModuleDescriptionSerializer` instances
- `IModuleDescriptionSerializerRegistry -> DefaultModuleDescriptionSerializerRegistry`

CLI DI registers:

- `IDebugReplIo -> ConsoleDebugReplIo`
- `DebugCommandDispatcher`
- `IDebugFrontend -> ConsoleDebugFrontend`

Client projects may add additional `IModuleDescriptionSerializer` registrations. The public serializer and immutable-tree interfaces are the supported extension surface; default implementation classes remain internal. Because `ICyborgCoreServices` is imported into service providers in other assemblies, the internal built-in implementations are registered through module factory methods that expose only the public serializer/registry contracts.

## Cancellation Semantics

Cancellation is propagated across every asynchronous debugger-description boundary:

- `IDebugFrontend.PauseAsync`
- `IDebugReplIo.ReadLineAsync`
- `IDebugPauseContext.InspectAsync`
- `IModuleDescriptor.DescribeAsync`
- `ModuleDescription.BuildAsync` / `SerializeAsync`
- `IModuleDescriptionSerializer.SerializeAsync`
- component visitor methods

The console dispatcher passes the pause token into inspection and checks it before/after CAF dispatch. No descriptor path synchronously blocks on a `Task` or `ValueTask`.

## Current Extension Points

| Goal | Extension point |
|---|---|
| Alternative debugger UI | Implement `IDebugFrontend` |
| Custom description format | Implement/register `IModuleDescriptionSerializer` |
| Format-specific handling such as redaction | Interpret component/property hint keys in the serializer |
| Module property description | Generated `IModuleDescriptor.DescribeAsync` |
| CLI debugger command | Add CAF command registration in internal `DebugCommandDispatcher` |
| Breakpoint matching/storage changes | `IBreakpointRegistry` / `BreakpointExpression` |

The debugger and description pipeline are intentionally still evolvable. Breaking improvements inside these subsystems are acceptable while they are WIP; this does not imply the same instability for unrelated core runtime contracts documented elsewhere.
