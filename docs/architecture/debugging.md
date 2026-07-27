# Workflow Debugging

This document describes the workflow debugging subsystem: breakpoint matching, the interactive debug session model, module inspection, and the adapter boundary that keeps console I/O out of the runtime engine. After reading this document, you should understand how `--break-at` pauses execution, how the REPL is structured for extension, and how per-module inspection is generated without requiring changes to individual module sources.

For the surrounding execution model, see [Architecture Overview](architecture-overview.md). For the source generator infrastructure that emits `Inspect` and identity formatting, see [Source Generators](source-generators.md).

**Table of Contents**

<!-- @import "[TOC]" {cmd="toc" depthFrom=2 depthTo=6 orderedList=false} -->

<!-- code_chunk_output -->

- [Overview](#overview)
- [Design Goals](#design-goals)
- [CLI Entry Point](#cli-entry-point)
- [Execution Boundary](#execution-boundary)
- [Breakpoint System](#breakpoint-system)
  - [Expression Matching](#expression-matching)
  - [Stepping](#stepping)
  - [Session Lifetime](#session-lifetime)
- [Interactive Session Model](#interactive-session-model)
  - [Frontend Adapter](#frontend-adapter)
  - [Pause Context](#pause-context)
  - [Console REPL](#console-repl)
  - [Built-in Commands](#built-in-commands)
  - [Extending the REPL](#extending-the-repl)
- [Module Identity and Inspection](#module-identity-and-inspection)
  - [Short Identity (ToString)](#short-identity-tostring)
  - [Full State Dump (Inspect)](#full-state-dump-inspect)
  - [Source Generation](#source-generation)
- [Runtime Integration](#runtime-integration)
  - [Service Registration](#service-registration)
  - [Inactive Path](#inactive-path)
  - [Cancellation Semantics](#cancellation-semantics)
- [Extension Points](#extension-points)
- [Key Decisions](#key-decisions)

<!-- /code_chunk_output -->


## Overview

Cyborg workflows are declarative module graphs. Debugging focuses on **execution boundaries between modules**, not the internal implementation of each worker. When a breakpoint matches, the engine has already loaded, initialized, and validated the module; the interactive session then allows the user to inspect configuration state, manage breakpoints, step through the graph, or cancel execution.

Modules remain atomic black boxes: the debugger never requires knowledge of worker internals. Inspection targets the validated module record (the immutable configuration object after defaults, overrides, and validation).

## Design Goals

1. **Caller-observable neutrality** — When no breakpoints are set, runtime behavior is unchanged (aside from a cheap enablement check per module).
2. **Adapter separation** — Console I/O lives in the CLI. The runtime exposes debugging core services and pause context only. Remote or web debuggers can plug in via `IDebugFrontend`.
3. **Zero module source churn** — Identity formatting and recursive inspection are source-generated from existing `[GeneratedModuleValidation]` annotations.
4. **Extensible REPL** — Commands are DI-registered handlers; new commands do not require modifying the frontend loop.
5. **Unified step/break matching** — Step is implemented as a one-shot `.*` breakpoint so stepping and breaking share the same matcher.

## CLI Entry Point

```bash
cyborg run --break-at <expression> --main /path/to/workflow.jconf
```

| Flag | Semantics |
|------|-----------|
| `--break-at <expression>` | Registers a breakpoint expression for the run. Repeatable. Expressions are regular expressions matched against module id, name, and group. |

When at least one expression is provided, the CLI:

1. Resolves `IWorkflowDebugger` and `IDebugFrontend` from DI.
2. Assigns the console frontend to the debugger.
3. Registers each expression in the breakpoint registry.
4. Executes the workflow as usual; the worker pipeline consults the debugger at each module boundary.

Without `--break-at`, the debugger remains registered but inactive (`IsEnabled == false`).

## Execution Boundary

The sole runtime hook is in `ModuleWorker<TModule>` after the validation pipeline completes and before the worker's abstract `ExecuteAsync` runs:

```
Load → ApplyDefaults → ResolveOverrides → Validate → [DEBUG HOOK] → ExecuteAsync → Exit
```

At the hook:

1. Resolve optional `IWorkflowDebugger` from DI.
2. If missing or `IsEnabled` is false, continue immediately.
3. Otherwise call `EvaluatePreExecutionAsync`.
4. On `DebugResumeAction.Cancel`, return `runtime.Exit(Canceled())` without executing the module.

This placement guarantees that `inspect` sees the fully validated module instance (defaults and overrides applied) while still allowing cancel before side effects.

## Breakpoint System

### Expression Matching

`IBreakpointRegistry` stores numbered `BreakpointExpression` entries. Each expression is compiled as a culture-invariant regular expression with a match timeout. A module matches when **any** of the following match the expression:

- Module id (e.g. `cyborg.modules.sequence.v1`)
- Module name (if present)
- Module group (if present)

Examples:

| Expression | Matches |
|------------|---------|
| `step-two` | Name or group containing `step-two` (substring regex) |
| `^step-two$` | Exact name/group `step-two` |
| `cyborg\.modules\.empty\.v1` | Empty module by id |
| `.*` | Every module (used for step) |

### Stepping

The `step` command calls `IDebugPauseContext.RequestStep()`, which registers a **one-shot** breakpoint with expression `.*` (`WorkflowDebugger.STEP_EXPRESSION`). On the next module boundary, the matcher consumes and removes that one-shot entry, then pauses again. Persistent breakpoints remain.

### Session Lifetime

| Action | Effect on registry |
|--------|--------------------|
| `break at <expr>` | Adds a persistent breakpoint |
| `break rm <n>` | Removes by id |
| `break ls` | Lists current entries (including one-shot step entries) |
| `detach` | Clears all breakpoints; subsequent modules do not pause |
| `continue` | Leaves registry unchanged |
| EOF on REPL input | Equivalent to detach + continue (safe for non-interactive pipes) |

## Interactive Session Model

### Frontend Adapter

```csharp
public interface IDebugFrontend
{
    ValueTask<DebugResumeAction> PauseAsync(IDebugPauseContext context, CancellationToken cancellationToken);
}
```

The runtime never reads from or writes to the console. It only invokes the frontend when a breakpoint matches. `DebugResumeAction` is either `Continue` or `Cancel`.

### Pause Context

`IDebugPauseContext` is the sole surface available to frontends during a pause:

| Member | Purpose |
|--------|---------|
| `Module` / `ModuleId` | Current module instance and id |
| `ModuleIdentity` | Short identity string (id/name/group) |
| `Runtime` | Ambient `IModuleRuntime` for future environment inspection |
| `Breakpoints` | Session breakpoint registry |
| `Inspect()` | Full state dump |
| `RequestStep()` / `Detach()` | Session control without resume-action variants |

### Console REPL

`ConsoleDebugFrontend` implements `IDebugFrontend` for interactive terminal use. On pause it:

1. Prints `Breakpoint hit: {ModuleIdentity}`.
2. Enters a prompt loop `(cyborg-dbg)`.
3. Dispatches lines to DI-registered `IDebugReplCommand` handlers.
4. Returns only when a command yields a resume action (`continue`, `step`, `detach`, `cancel`) or on EOF.

I/O is abstracted behind `IDebugReplIo` (`ConsoleDebugReplIo` for production, `TextDebugReplIo` for tests and scripted automation).

### Built-in Commands

| Command | Aliases | Behavior |
|---------|---------|----------|
| `continue` | `c`, `cont` | Resume until the next matching breakpoint |
| `step` | `s` | Register one-shot `.*`, resume, break at next module |
| `detach` | | Clear all breakpoints and resume |
| `inspect` | `i` | Print full validated module state |
| `break at <expr>` | `b at …` | Add a breakpoint |
| `break ls` | `b ls` | List breakpoints |
| `break rm <n>` | `b rm …` | Remove breakpoint by number |
| `cancel` | `q`, `quit` | Cancel the current module (workflow cancellation path) |
| `help` | `h`, `?` | List commands (handled by the frontend) |

### Extending the REPL

Additional commands implement `IDebugReplCommand` and register as:

```csharp
[Singleton<IDebugReplCommand, MyNewCommand>]
```

in the CLI composition module. The frontend discovers all handlers via `IEnumerable<IDebugReplCommand>`. Commands that need I/O take `IDebugReplIo`; commands that need richer runtime state use `IDebugPauseContext.Runtime` or future context extensions.

Examples of future commands that fit this model without runtime changes:

- Inspect environment variables
- Set/remove variables
- Mutate module properties (if a mutation API is added to the context)
- Conditional breakpoints

## Module Identity and Inspection

### Short Identity (ToString)

Generated modules override `ToString()` via `ModuleIdentity.Format(moduleId, name, group)`, producing compact banners such as:

```
cyborg.modules.empty.v1 name=step-two
cyborg.modules.sequence.v1 name=root-sequence group=backup
```

### Full State Dump (Inspect)

Modules implement `IInspectable.Inspect()`, returning a multi-line dump of identity plus property values. Nested graphs use `ModuleInspection` helpers that dispatch without reflection (AOT-safe):

- `IInspectable` → recursive `Inspect()`
- `ModuleReference` / `IModuleWorker` → unwrap to module
- `ModuleContext` → format module, environment, configuration, requires
- Collections → indexed elements
- Scalars → quoted/invariant formatting
- Other records → default `ToString()`

### Source Generation

The validation generator (`[GeneratedModuleValidation]`) emits both pipeline methods and inspection members:

- Implements `IInspectable` on the partial module record
- Generates `ToString()` and `Inspect()`
- Walks the same property model used for validation, so nested configuration stays in sync

No per-module manual annotations beyond the existing validation attribute are required. Nested types marked only `[Validatable]` rely on record `ToString` or recursive property walking through the parent dump.

`IInspectable` is registered as `ModuleValidationGeneratorContract.IInspectable` so the generator discovers it through the existing contract system.

## Runtime Integration

### Service Registration

| Service | Project | Lifetime |
|---------|---------|----------|
| `IBreakpointRegistry` → `BreakpointRegistry` | Core | Singleton (per process / DI root) |
| `IWorkflowDebugger` → `WorkflowDebugger` | Core | Singleton |
| `IDebugFrontend` → `ConsoleDebugFrontend` | CLI | Singleton |
| `IDebugReplIo` → `ConsoleDebugReplIo` | CLI | Singleton |
| `IDebugReplCommand` implementations | CLI | Singleton (multiple) |

The CLI assigns `debugger.Frontend` only when `--break-at` is present, so a headless host can register breakpoints with a custom frontend or none at all.

### Inactive Path

Per module execution when debugging is unused:

1. `GetService<IWorkflowDebugger>()` — null if the host never registered it, or a live instance from Core DI.
2. If non-null, read `IsEnabled` (`Breakpoints.Count > 0`).
3. Return to `ExecuteAsync` with no further work.

Space/time cost is intentionally small; no allocations occur on the inactive path beyond the service lookup.

### Cancellation Semantics

`cancel` maps to `DebugResumeAction.Cancel`. The worker returns `runtime.Exit(Canceled())` for the **current** module without running it. Parent modules (e.g. sequence) observe `ModuleExitStatus.Canceled` through normal result propagation. External `CancellationToken` cancellation still applies during the REPL wait and aborts the pause with the token's exception path.

## Extension Points

Prefer these hooks over modifying `ModuleRuntimeBase` or individual workers:

| Extension | Mechanism |
|-----------|-----------|
| New REPL command | `IDebugReplCommand` + DI registration |
| Alternate UI (remote, web, IDE) | Implement `IDebugFrontend` |
| Scripted / test I/O | Implement `IDebugReplIo` |
| Custom breakpoint sources | Populate `IBreakpointRegistry` before `ExecuteAsync` |
| Richer pause data | Extend `IDebugPauseContext` (new members) while keeping existing ones stable |
| Inspection formatting | Extend `ModuleInspection` dispatch or generated property walk |

The debug hook in `ModuleWorker` should remain the only execution-pipeline integration point unless a future feature requires pre-validation pauses.

## Key Decisions

1. **Post-validation hook** — Users debug configuration as the worker will see it; cancel still prevents execution side effects.
2. **Optional debugger service** — Hosts without debugging pay only for a null/disabled check; CLI always registers Core services but activates the frontend only with `--break-at`.
3. **Regex breakpoints** — One expression language covers id, name, group, and step (`.*`), including future patterns such as `backup-.*`.
4. **One-shot step breakpoints** — Avoids a parallel step-mode flag that would fork matching logic; step appears in `break ls` while active.
5. **Generated Inspect on the validation attribute** — Reuses the property model and avoids a second opt-in attribute on every module.
6. **Command handlers in CLI** — Keeps ConsoleAppFramework and System.Console concerns out of `Cyborg.Core`, enabling non-console adapters without optional references.
7. **EOF detaches** — Prevents deadlocks when stdin is a closed pipe in automation; production interactive use always has a TTY.

