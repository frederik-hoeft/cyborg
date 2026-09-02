# Workflow Debugging and Module Descriptions

This document describes the workflow debugging subsystem and the format-neutral module-description pipeline used by debugger inspection and other presentation clients.

For the surrounding invocation, transaction, and lifecycle model, see [Architecture Overview](architecture-overview.md) and [Transactional Execution](transactions.md). For the source generation that produces rich module descriptors, see [Source Generators](source-generators.md).

## Overview

Cyborg debugging operates at prepared module execution boundaries. A breakpoint is evaluated after defaults, override resolution, interpolation, and constraint evaluation have produced an `IValidationResult<TModule>`, but before that result is enforced and before the worker executes. The debugger can therefore inspect the prepared module together with any validation errors, including configurations that would fail normal execution.

The debugger combines four kinds of state with deliberately different ownership:

- persistent breakpoint expressions are process-wide debugger-session state;
- step state follows the transaction branch of the paused invocation;
- the live execution topology is a Core-owned projection of currently open structured module invocations;
- frontend ownership is serialized by a debugger pause coordinator so only one interactive frontend session is active at a time.

`Cyborg.Core` owns these runtime mechanics and exposes frontend-neutral pause state through `IDebugFrontend` and `IDebugPauseContext`. `Cyborg.Cli.Debugging` provides the console frontend, command surface, and text rendering for execution trees and ancestry. `Cyborg.Cli` is the host composition root: it registers the CLI debugger services and selects runtime configuration sources.

Module descriptions remain independent of debugger control. `IModuleDescriptionSerializer` is the format extension boundary, so applications can register additional output formats through DI and reuse the same descriptor tree outside a debugging session.

## Execution Boundary and Identity

Every runtime-owned module invocation carries a stable `ModuleExecutionId` and an optional parent execution ID. All runtime views that belong to the same invocation reuse that identity. Nested execution creates a child identity from the structured caller rather than inferring ancestry from a CLR thread, `AsyncLocal<T>`, or runtime-object discovery.

The runtime exposes a general-purpose execution-lifecycle observer independently of module validation/pre/post hooks:

```text
invocation scope created
  -> Started(execution id, parent id, initial module identity)
  -> environment/configuration/main module work
       -> generated preparation and validation
       -> validation hooks
       -> pre-execution hooks [debugger may pause]
       -> EnsureValid
       -> worker execution
       -> post-execution hooks
  -> Completed(result)              // only after a definite result exists
  -> transaction reconciliation/discard
  -> Closed(joined/discarded)
  -> invocation scope disposal
```

`Started` is early enough to observe invocations that fail before the module pre-execution boundary. `Completed` records a definite module result while the invocation may still be structurally open, and `Closed` marks the point after reconciliation or discard when that invocation no longer belongs in a current-state execution topology. Lifecycle observers are isolated from workflow execution: an observer failure is logged and does not change the module result, reconciliation, or delivery to later observers.

The workflow debugger itself participates through the normal pre-execution hook. The validation result carried into the debugger always contains the prepared module. Returning `Continue` resumes the normal lifecycle; returning `Cancel` lets the debugging hook produce a canceled module result without invoking the worker. `Step` and `Detach` are debugger control actions interpreted centrally by the workflow debugger rather than mutations performed by the frontend.

## Breakpoints and Branch-Scoped Stepping

`IBreakpointRegistry` stores numbered regular-expression breakpoints. Expressions are culture-invariant and matched against the module ID plus `Name` and `Group` when present. Persistent breakpoints are global across execution branches and remain registered until explicitly removed or detached. The registry also supports one-shot expressions as a general feature, but built-in stepping does not use a wildcard breakpoint.

Persistent expressions are evaluated in breakpoint-ID order. One-shot expressions are evaluated first, newest first, and a matching one-shot is atomically consumed by the caller that wins its removal. A persistent expression remains registered after a match. Regular-expression matching has a bounded timeout; a timeout pauses execution with a debugger diagnostic rather than failing the workflow. A timed-out persistent expression remains registered, while a one-shot is consumed when its evaluation causes that pause.

| Expression | Meaning |
|---|---|
| `step-two` | Substring match against ID/name/group |
| `^step-two$` | Exact name/group match |
| `cyborg\.modules\.empty\.v1` | Match the empty module ID |
| `.*` | Match every module |

At each prepared module boundary, the debugger evaluates two independent inputs:

```text
should pause = persistent/one-shot breakpoint decision
               OR current branch is stepping
```

There is no process-wide `IsEnabled` mirror for branch stepping. The pre-execution hook resolves the transaction-scoped `IDebugBranchControl` from the current invocation provider and performs the cheap branch-state/breakpoint check directly.

### Step propagation

Step state is transaction-aware execution-control state. A child invocation inherits the step state of the transaction branch from which it forks. Sibling branches receive isolated copies and can independently choose `Step` or `Continue`.

When a fork generation reconciles, the restored owner state is derived from the child contributors rather than from the frozen pre-fork owner continuation:

```text
owner stepping after join = any non-stale child remains stepping
```

This gives the following behavior:

- stepping a sequential child causes the next child invocation on that branch to pause;
- stepping into a nested or dynamic module follows that structured descendant;
- stepping one parallel branch does not implicitly step unrelated siblings;
- `Continue` clears stepping only for the branch represented by that pause;
- if every child of a parallel generation continues, stepping is cleared when the owner resumes after join;
- if any current-generation child remains stepping, the owner resumes in step mode and the next invocation on that restored branch pauses;
- persistent breakpoint matches remain global and can pause an unrelated branch without consuming another branch's step state.

The debugger session has a monotonically increasing generation used as a fencing token for branch-control state. `Detach` advances the generation so transactional state already copied into live branches becomes stale without requiring the debugger to discover and mutate every transaction instance. During reconciliation, only contributors from the newest represented generation can restore step state.

## Pause Coordination

Parallel execution can decide to pause on several branches concurrently. Breakpoint matching and branch-step evaluation happen on the executing branch before frontend ownership is requested. Once a boundary has decided to pause, ordinary breakpoint mutation does not retroactively revoke that decision.

`DebugPauseCoordinator` serializes frontend ownership with FIFO semantics:

```text
branch decides to pause
  -> mark execution as paused
  -> enqueue/acquire frontend ownership
  -> mark owner as current
  -> frontend session
  -> release ownership
  -> restore running state and promote next valid queued pause
```

Only one frontend session is active. Other decided pauses remain logically paused and visible in the execution topology while they wait. Admission and release share one coordinator synchronization boundary, so a pause arriving while another session resumes is either queued before release or acquires the newly free slot; it is not lost between a separate queue check and resume decision.

Deleting a breakpoint does not un-pause a branch that already matched it. `Detach` has stronger semantics because it invalidates the debugger session itself: it clears global breakpoints, advances the session generation, clears the current branch's effective step state, and suppresses queued pauses that belong to the invalidated generation. Cancellation of a queued execution removes its queue request and restores its topology state without preventing later valid requests from acquiring the frontend.

## Live Execution Topology

`IDebugExecutionTopology` is the read-only Core boundary for the debugger's current logical execution topology. It is populated by the general execution-lifecycle observer and keyed by explicit `ModuleExecutionId` values.

A node is created on `Started`, before generated preparation or pre-execution hooks are required to succeed. The debugging pre-execution hook enriches the node with the prepared module's final `Name` and `Group` when that boundary is reached. `Completed` records the exit status but retains the node until `Closed`, which means a completed parallel sibling remains visible while other siblings are still open. `Closed` removes the invocation from the live topology.

The topology is deliberately a current-state model, not a trace. Once a structured invocation closes, its node is pruned. Consumers that require execution history should build that concern as a separate lifecycle observer rather than keeping closed debugger nodes indefinitely.

Open nodes expose these states:

| State | Meaning |
|---|---|
| `running` | The invocation is active and has not produced a definite result |
| `completed: <status>` | A definite result exists, but the structured invocation has not closed yet |
| `paused` | The invocation decided to pause and is waiting for frontend ownership |
| `paused/current` | The invocation currently owns the frontend session |

`CaptureTree()` returns an immutable point-in-time forest of open invocations. `CaptureAncestry(executionId)` returns the selected invocation followed by its explicit logical ancestors up to the root. These projections do not expose the mutable internal topology.

## Frontend Boundary

The host-facing frontend contract remains small:

```csharp
public interface IDebugFrontend : IKeyedService
{
    ValueTask<DebugResumeAction> PauseAsync(IDebugPauseContext context, CancellationToken cancellationToken);
}
```

Frontend selection uses the keyed-service setting `cyborg.core.debug.frontend`. Core has no implicit frontend (`DebugOptions.Default.Frontend` is `null`) because presentation policy belongs to the host. `Cyborg.Cli.Debugging` registers the built-in `console` frontend, while the CLI composition root supplies `console` as its host default; ordinary configuration sources can replace that selection.

A frontend returns one of four dispositions:

| Action | Meaning |
|---|---|
| `Continue` | Clear stepping on the current branch and resume |
| `Step` | Resume with the current branch left in step mode |
| `Cancel` | Clear stepping and cancel the current module before worker execution |
| `Detach` | End the debugger session, clear breakpoints, invalidate branch-local debugger state, and resume |

`IDebugPauseContext` exposes the state that is valid while the frontend owns a pause:

| Member | Purpose |
|---|---|
| `ModuleId` | Canonical versioned ID of the paused module |
| `ExecutionId` | Stable logical invocation ID when the context belongs to runtime execution |
| `ValidationResult` | Prepared module, validity state, and validation errors |
| `Runtime` | Runtime associated with the paused execution boundary |
| `Services` | Invocation service provider used as the fallback for frontend command DI |
| `Breakpoints` | Global debugger-session breakpoint registry |
| `Diagnostics` | Debugger-side diagnostics associated with entering this pause |
| `Tree` | Fresh immutable snapshot of all currently open logical executions |
| `Stack` | Fresh immutable ancestry projection for the paused execution |

Runtime-provided pause contexts capture `Tree` and `Stack` on each access. A long-running frontend can therefore observe siblings that progress from running to paused or completed while the current session remains open. Custom contexts that are not attached to a runtime invocation can expose no execution ID and use the empty default projections.

The frontend does not mutate transactional stepping state directly. This keeps presentation adapters independent from transaction mechanics and gives `WorkflowDebugger` one place to apply resume actions, session invalidation, and branch-control changes.

## Console REPL and CAF Isolation

`ConsoleDebugFrontend` owns the interactive pause lifecycle: display the pause state and debugger diagnostics, read a prompt-aware command line through `IDebugReplIo`, dispatch it, and continue until a command returns a resume action. EOF returns `Detach`, allowing the workflow debugger to perform the same centralized session cleanup as the explicit command. Inspection serializes the prepared module descriptor and then reports associated validation errors.

The console frontend uses ConsoleAppFramework (CAF) for command routing, aliases, argument binding, validation, generated help, and command dependency injection. Cyborg retains only a lexical tokenizer because an interactive REPL receives one input string while CAF consumes an argument vector. Quoting and escaping are handled before CAF dispatch, while command grammar remains CAF-owned.

The process CLI and debugger command surfaces live in separate compilations:

```text
Cyborg.Cli
  main CAF command surface (`run`, ...)

Cyborg.Cli.Debugging
  debugger CAF command surface (`continue`, `step`, `tree`, `stack`, `break`, ...)
```

This isolation prevents debugger help and routing from exposing or recursively invoking process-level commands. During one debugger command dispatch, pause-local objects are layered over the invocation service provider so command classes can receive both kinds of dependencies through constructor injection.

`IDebugReplIo` is the console presentation extension boundary. It owns prompt-aware reads and semantic writes classified by `OutputKind` (`Text`, `Status`, `Success`, `Warning`, and `Error`). Core topology objects carry no text-formatting policy; `ExecutionTreeFormatter` and the `tree`/`stack` commands live in `Cyborg.Cli.Debugging`.

### Built-in commands

| Command | Aliases | Behavior |
|---|---|---|
| `continue` | `c`, `resume` | Clear step mode on this branch and resume until another breakpoint/step boundary |
| `step` | `s` | Resume with this execution branch in step mode |
| `detach` | none | End the debugger session and resume execution |
| `cancel` | `q`, `quit` | Cancel the paused module before its worker executes |
| `inspect` | `i` | Serialize the prepared module descriptor and print associated validation errors |
| `tree` | none | Render the current live logical execution tree |
| `stack` | none | Render the current invocation followed by its logical ancestors |
| `break at <expression>` | `b at ...` | Add a persistent breakpoint |
| `break ls` | `break list`, `b ls`, `b list` | List breakpoints |
| `break rm <id>` | `break remove`, `b rm`, `b remove` | Remove one breakpoint |
| `help [command]` | `h`, `?` | Display CAF-generated debugger help |

`tree` distinguishes running, completed-but-open, queued paused, and current paused invocations. `stack` numbers frames from the current invocation (`#0`) toward the root. Empty views are rendered explicitly rather than as blank output.

## Module Identity and Descriptor Capability

`IModule` defines the runtime identity and inspection surface shared by all modules: `Name`, `Group`, and `GetDescriptor()`. `IModuleDefinition` adds the static versioned `ModuleId` used for loading and execution. Short identity strings combine these values for diagnostics, breakpoint banners, and topology rendering without relying on the concrete module type.

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

Description properties and values may carry `ImmutableArray<string>` hints. Hints are arbitrary metadata keys with no mandatory semantics in the description tree. The tree preserves them for custom serializers and other downstream consumers. Built-in serializers do not reinterpret hints as tagged-value metadata, keeping presentation hints separate from taint state.

`TaggedString` values are first-class atoms. Built-in text and JSON serializers render their runtime tags through `ITaggedStringRenderer`; a value carrying `cyborg.secret.v1` is therefore written as `[REDACTED]` rather than the raw secret. `[Secret]` establishes that tag during generated preparation, so debugger and validation inspection of prepared modules relies on the same tagged value state used by every other Cyborg presentation surface.

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
  global breakpoint registry
  debugger session generation
  transactional branch-control participant + scoped facade
  workflow debugger / FIFO pause coordinator
  live execution topology
  frontend selection service
  pre-execution debugging hook
  execution-lifecycle topology observer

ICyborgCliDebugServices (Cyborg.Cli.Debugging)
  console REPL I/O
  keyed console debug frontend
  CLI breakpoint argument integration
  tree/stack rendering and command surface
```

The execution lifecycle observer is a general runtime extension point; only the registered topology observer is debugger-specific. Transaction participation is likewise provided by the generic transaction-aware service infrastructure, while the debugger defines only its branch-state merge semantics.

This split keeps runtime observation/control, module-description serialization, and host presentation independently replaceable. Core defines execution identity, current-state projections, and debugger orchestration; the CLI debugger assembly owns console-specific behavior; the application composition root decides which frontend and configuration sources are active.

## Testing Expectations

Debugger tests should preserve architectural boundaries rather than merely command implementations. Core coverage owns execution identity/lifecycle ordering, topology snapshot semantics, branch-control fork/join rules, pause-coordinator FIFO/cancellation/session invalidation, breakpoint evaluation diagnostics, and workflow-debugger action application. CLI coverage owns command registration, aliases/tokenization, tree/stack rendering, prompt-aware I/O, semantic output categories, inspection, and EOF behavior.

Production-flow integration coverage should exercise the same model through real control-flow modules: sequential and dynamic nested calls, parallel descendant stepping, independent sibling step/continue decisions, global breakpoint hits alongside branch-local stepping, join restoration after all/some descendants continue, failures before the main debugger boundary, and forced queued-pause detach/cancellation scenarios.

Module-description coverage should exercise generated scalar/nested/collection traversal, nullable and default collection shapes, hint preservation, tagged-value rendering, custom serializer registration, and cancellation.
