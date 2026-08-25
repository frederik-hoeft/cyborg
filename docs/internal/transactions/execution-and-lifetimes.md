# Execution and Lifetimes

> **Status:** Internal target design. See [Transactional Execution Design](README.md) for scope and invariants.

## Responsibility

The execution subsystem owns module invocation lifetime. It establishes the transaction and DI scope before any invocation-specific services are resolved, executes the complete `ModuleContext` lifecycle inside that boundary, and closes nested execution in structured order.

A transaction and DI scope are created together for an invocation, but they solve different problems:

- the transaction supplies inherited workflow state, isolation, and reconciliation;
- the DI scope supplies object lifetime and dependency resolution;
- the runtime exposes execution operations and environment views inside those two boundaries.

Neither DI scope ancestry nor runtime-object ancestry is used as a substitute for transaction ancestry.

## Loaded Module Graph

Deserialization produces an immutable executable graph. A loaded module reference conceptually contains:

```text
loaded module reference
  immutable module definition
  module/loader identity
  generated activation metadata
```

It does not contain a stateful worker or dependencies resolved from the service provider that happened to perform deserialization.

The loader registry remains immutable application infrastructure used to select AOT-known module metadata from a versioned module ID. Loading a graph may also produce immutable execution seed data such as statically declared named modules, but loading does not mutate process-global runtime state.

### Activation boundary

Worker activation happens at execution time. Generated activation continues to analyze worker constructors at compile time and emits direct construction code, but the generated activator receives the service provider for the current module invocation.

This preserves native-AOT guarantees while ensuring that:

- the worker itself is invocation-local;
- `IWorkerContext<TModule>` is invocation-local;
- scoped constructor dependencies come from the current module scope;
- repeated or concurrent execution of the same loaded definition never aliases worker fields or scoped dependencies.

A worker may therefore safely retain mutable fields such as the prepared module, result builder, and artifact builder for the lifetime of one invocation.

## Execution Sessions and Roots

Application infrastructure is process-wide where appropriate, but workflow state begins at an execution session.

A root execution session creates:

- an immutable execution seed;
- a parentless root transaction;
- a fresh root-module DI scope;
- transaction-bound runtime and environment views.

Multiple sessions can coexist under one application provider:

```text
application provider
  +-- execution A
  |     +-- root transaction A
  |     +-- root module scope A
  +-- execution B
        +-- root transaction B
        +-- root module scope B
```

The sessions share ordinary singletons but not workflow-semantic state. The root transaction has nowhere implicit to commit when it terminates; its final state can be inspected, exported, or discarded by the execution host.

## ModuleContext as the Invocation Boundary

`ModuleContext` remains the execution envelope around a module definition, environment selection, optional configuration module, and requirements. Executing a context establishes one invocation transaction/DI scope for the main module lifecycle.

Within that invocation:

1. derive the invocation transaction from the caller's current effective state;
2. create a fresh DI scope and bind it to that transaction;
3. prepare or select the invocation environment inside the transaction;
4. resolve required arguments and write any normalized invocation-local values into that transaction;
5. execute the optional configuration module as a nested child invocation and reconcile it before the main module is prepared;
6. activate the main worker from the invocation scope;
7. run generated preparation, validation, lifecycle hooks, and worker execution;
8. finalize and publish artifacts through the current transaction's environment state;
9. return the immutable module result to the caller;
10. let the caller's structured execution policy reconcile or discard the completed transaction;
11. dispose the invocation DI scope after all nested work has terminated.

Direct internal execution of a loaded module definition without a full `ModuleContext` follows the same invocation boundary with default context semantics. There is never a separate reusable worker lifetime hidden underneath this path.

## DI Scope Creation

Module scopes are created from the DI scope-creation service contract, not by assuming that the current `IServiceProvider` object also implements the factory interface. This matters for Jab and MEDI: child scopes are service providers, but DI scopes are not a hierarchical provider tree from which scoped instances should be inherited.

Conceptually, one application-level execution-scope facility is responsible for:

1. obtaining the application `IServiceScopeFactory` through DI;
2. creating one fresh scope per invocation;
3. initializing the scoped transaction/execution context before worker dependencies are resolved;
4. exposing the initialized provider to generated activation;
5. disposing the scope after the invocation and every nested execution it owns has completed.

There is no production fallback to an unscoped provider. Test infrastructure must preserve the same invariant even if its underlying provider implementation differs from Jab.

## Scoped Transaction Context

The current transaction is normal scoped execution state. Runtime services and transaction-aware service facades resolve it through the invocation DI scope.

A process-wide `AsyncLocal` transaction pointer is not required for normal routing. The runtime already owns explicit structured invocation boundaries, and DI already provides an unambiguous lifetime boundary for invocation-local context.

A stable scoped transaction context may internally point at the current branch state while a fork group is open. This allows future parent-continuation execution without replacing existing scoped service objects. The branch routing remains an explicit property of the execution scope rather than ambient process state inherited by arbitrary tasks.

## Sequential Child Execution

Every nested module call uses structured fork/join even when there is only one child:

```text
parent invocation transaction
  |
  +-- open one-child fork group from current effective state
  +-- create child invocation transaction and DI scope
  +-- execute complete child lifecycle
  +-- prepare and publish successful reconciliation
  +-- dispose child scope
  +-- resume parent invocation with updated effective state
```

The next child forks from that updated state. Changes reconciled from earlier children remain part of the parent's durable parent-relative change set until the parent itself terminates.

Existing composite modules such as `Sequence`, `ForEach`, `If`, `While`, `Guard`, configuration modules, and named-module references should not implement transaction logic. They continue to express control flow by invoking the runtime; the runtime supplies the same transactional child semantics everywhere.

## Parallel Child Execution

`cyborg.modules.parallel.v1` is the first multi-sibling consumer. Its worker asks the runtime to open one fork group for all branch contexts:

1. capture one stable effective baseline;
2. create one child transaction and DI scope per branch;
3. execute each complete branch `ModuleContext` concurrently;
4. await every started branch;
5. prepare reconciliation across all branches and all transactional components;
6. publish one candidate owner state if preparation succeeds, otherwise publish nothing;
7. dispose all branch scopes.

Sibling ordering for reconciliation is structural, such as declaration order, not task-completion order. Successful non-conflicting state must therefore be deterministic regardless of scheduler timing.

The parent continuation is empty for the initial `Parallel` module. The transaction core still models a continuation branch so later structured background execution can reuse the same fork/join semantics without redefining isolation.

## Future Structured Consumers

Later execution features should compose from the same invocation and fork-group boundaries rather than introducing separate state models:

- **Retry:** each attempt executes in a fresh child transaction/DI scope; rejected attempts can be discarded and only the accepted attempt reconciled.
- **Managed background/sidecar work:** a fork group keeps explicit child branches plus a live owner continuation, then reconciles all surviving contributors at the owning structured join point.
- **Commit/rollback policy:** a completed child transaction can be reconciled or discarded according to invocation policy without changing how the child obtained isolation.

These consumers may add policy, but they do not change worker activation, baseline/change ownership, component atomicity, or DI lifetime semantics.

## Cancellation and Structured Lifetime

Cancellation is a control signal, not a transaction merge operation.

- Caller cancellation propagates to runtime-owned child executions.
- Every child started by a fork group is observed before that group closes.
- A transaction and DI scope cannot terminate while they own unfinished nested execution.
- Cancellation can prevent reconciliation from starting, but cannot interrupt atomic state publication halfway through.
- Ordinary branch failure does not implicitly cancel unrelated siblings unless the control-flow module explicitly defines fail-fast behavior.
- Arbitrary tasks spawned directly by module code are outside Cyborg's structured execution contract and must not retain invocation-scoped services beyond module lifetime.

## Result Ownership

Module execution results are immutable execution outcomes, not transactional state components. A structured child handle associates a completed result with the child transaction that produced it.

Reconciliation determines whether workflow-semantic state can be incorporated. Control-flow modules determine how child statuses map to their own status. The initial `Parallel` module should aggregate status deterministically using the established Cyborg control-flow status conventions while retaining branch results in declaration order where branch-specific reporting is required.

A `Failed` or `Canceled` module result does not automatically imply rollback in the first migration stage. Commit/discard based on result status is a later invocation policy that can reuse the same completed child transaction boundary.

## Subsystem Responsibilities

The target architecture divides responsibilities across existing projects rather than introducing transaction behavior into module-specific code.

| Project / subsystem | Responsibility |
|---|---|
| `Cyborg.Core` module configuration/loading | Immutable loaded definitions, versioned loader lookup, load-time graph/seed construction, and execution-time activation metadata. |
| `Cyborg.Core` runtime execution | Execution sessions, invocation scopes, transaction ancestry, fork/join ownership, lifecycle ordering, cancellation, and result handoff. |
| `Cyborg.Core` transactional state | Component registration, baseline/change ownership, conflict preparation, and atomic aggregate publication. |
| `Cyborg.Core` environment runtime | Transaction-bound environment views and the built-in environment transactional component. |
| `Cyborg.Core.Aot` module-loader generator | Direct AOT-safe worker activation against the current invocation provider instead of load-time worker construction. |
| `Cyborg.Modules` control flow | Express sequential/conditional/parallel execution through runtime APIs without implementing state isolation itself. |
| `Cyborg.Cli` composition root | Process-wide application services and creation of independent workflow execution sessions; no singleton mutable workflow root. |
| `Cyborg.Core.TestAdapter` / tests | Production-equivalent per-invocation scopes plus focused transaction, activation, and compatibility assertions. |
| `Cyborg.Cli.Debugging` | Thread-safe/basic deterministic behavior only for the first migration; branch-aware UX remains separate. |
