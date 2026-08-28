# Transactional Execution Design

> **Status:** Internal architecture notes
>
> **Scope:** Detailed design rationale for the implemented transactional execution model. The steady-state production documentation under [`/docs/architecture`](../../architecture.md) is authoritative for supported behavior and extension contracts.

## Purpose

Cyborg needs a runtime model in which nested and concurrent module executions cannot expose scheduler-dependent mutations through workflow-owned state. Transactionality is therefore an execution primitive rather than behavior owned by a specific control-flow module.

Every module invocation executes in its own transaction and DI scope. Nested invocations derive from a stable view of their caller, remain isolated while running, and reconcile through structured join points. Sequential composition uses the same fork/join machinery as parallel composition; `Parallel` is only the first consumer that creates multiple sibling branches from one fork point.

The design covers Cyborg-managed workflow state and state from services that explicitly opt into transactional participation. It does not virtualize arbitrary process state, external I/O, ordinary singleton internals, or mutations inside object graphs stored as environment values.

## Document Map

| Document | Focus |
|---|---|
| [Execution and lifetimes](execution-and-lifetimes.md) | Module activation, execution sessions, DI scopes, invocation lifecycle, structured concurrency, and project responsibilities. |
| [Runtime responsibility boundaries](runtime-responsibilities.md) | Internal execution/environment responsibilities and the stable consumer-facing runtime boundary. |
| [State and reconciliation](state-and-reconciliation.md) | Transaction tree, baseline/change semantics, persistent state, fork groups, conflict detection, and atomic joins. |
| [Transaction core](transaction-core.md) | Generic participant descriptors/state, root seeds, fork-group lifecycle, conflict strategy, and aggregate publication. |
| [Transactional services](transactional-services.md) | Opt-in state participation for runtime and custom DI services, scoped access, root seeding, and lifetime boundaries. |
| [Environment and runtime state](environment-and-runtime-state.md) | Transactional environment graph, logical global state, named environments, artifacts, and named-module state. |
| [Transactional environment bindings](environment-bindings.md) | Logical environment identities, transaction-bound variable stores, and nested reconciliation. |
| [Named-module registry](named-module-registry.md) | Immutable configuration-load seeds, scoped registry facade, transactional registration/removal, and root isolation. |

## Execution Model

Four relationships coexist during execution and must remain distinct:

```text
module execution ownership       transaction ancestry
root invocation                  root transaction
  +-- child invocation             +-- child transaction
  +-- parallel invocation          +-- fork group
        +-- branch A                      +-- branch A transaction
        +-- branch B                      +-- branch B transaction

DI lifetimes                    environment topology
application provider            logical global environment
  +-- module scope A              +-- inherited environment A
  +-- module scope B                    +-- inherited environment B
  +-- module scope C              name "session" -> environment B
```

The module execution tree defines structured lifetime ownership. The transaction tree defines state ancestry and reconciliation. DI scopes define service-object lifetime and are not themselves hierarchical state containers. The environment graph defines variable inheritance and named environment identity inside transactional state.

## Core Invariants

The following invariants define the design independently of implementation details:

- A loaded module graph contains immutable module definitions and activation metadata, never invocation-specific worker state.
- A module worker and all scope-sensitive constructor dependencies are created only after the invocation transaction and DI scope exist.
- Every module invocation receives a fresh DI scope. Failure to establish that scope is an execution error; execution never silently falls back to a caller or application-root scope.
- A transaction owns only its local changes. Its baseline is an immutable effective snapshot captured from its parent at fork time.
- There is no mutable canonical workflow-state store beneath the transaction tree. Root transactions are ordinary parentless transactions initialized from immutable execution seeds.
- Forking is non-destructive. Existing parent-relative change provenance is never cleared, rebased away, or inferred from value equality.
- A successful explicit write or removal remains part of the transaction's change set even if later operations restore the visible baseline value or absence state.
- Siblings from one fork group share the same stable fork-time baseline and cannot observe sibling or post-fork continuation changes before reconciliation.
- Nested fork groups close before their owners can terminate. Runtime-owned child executions cannot outlive the structure that created them.
- Transactional components prepare reconciliation without mutating parent-visible state. A join publishes the complete candidate state or publishes nothing.
- Transactional propagation is opt-in for DI services and separate from ordinary singleton/scoped/transient lifetime semantics.
- Logical `Global` environment state is global only within one execution tree. Concurrent root executions from the same application provider are isolated.
- Environment values are opaque. Transactionality applies to bindings and runtime-owned topology, not to mutation inside referenced objects.

## Transactional State in One Sentence

Transactional state is represented recursively as an immutable fork-time baseline plus a transaction-local change set. There is no mutable canonical state beneath the transaction tree. Joining reconciles a child's changes into its parent's change set; it does not commit to an external or global backing store. Change entries may be compacted to their final operation, but explicit modification provenance is preserved until the transaction itself terminates.

This recursive model means a baseline is itself the effective result of an ancestor baseline plus ancestor changes. The root simply terminates the recursion with an immutable execution seed.

## Design Properties

The architecture provides:

1. first-class transactional execution for every module invocation;
2. stable snapshot isolation for child and sibling execution;
3. recursive change composition through arbitrary nesting;
4. all-or-nothing reconciliation across all transactional state participants;
5. independent root executions without process-global mutable workflow state;
6. correct per-invocation DI lifetimes and execution-time worker activation;
7. a reusable participation model for runtime and custom service state;
8. AOT-safe, source-generated activation without reflection fallback;
9. compatibility with existing sequential control-flow semantics;
10. structured concurrency suitable for later retry, background, and commit/rollback consumers.

## Current Boundaries

The transaction model does not provide:

- automatic rollback based on module result status;
- compensating actions for external side effects;
- deep cloning or merging of arbitrary environment values;
- serializable database-style transactions or read-set conflict detection;
- arbitrary deep-merge or last-writer-wins policies beyond an extensible conflict-strategy boundary;
- debugger branch/stepping UX beyond deterministic and thread-safe basic behavior;
- automatic transactional semantics for every scoped or singleton service.

## Non-Viable Shortcuts

Several superficially smaller changes do not establish the required model:

- replacing mutable workflow dictionaries with concurrent dictionaries provides thread safety, not snapshot isolation;
- deep-cloning environment values tries to virtualize consumer-owned object graphs and still does not define recursive reconciliation;
- keeping load-time workers while overlaying a second execution provider leaves constructor dependencies and worker state bound to the wrong lifetime;
- routing normal transactional state through process-wide ambient `AsyncLocal` state duplicates DI scope propagation and lets unrelated tasks inherit execution authority accidentally;
- allowing participants to mutate the parent one by one during join makes cross-component atomicity impossible;
- retaining a singleton mutable root/global environment and treating transactions as views over it prevents independent root executions;
- resolving `Parent`/named artifact targets by reaching into another runtime's live environment bypasses transaction isolation.

These constraints are architectural rather than implementation-style preferences. Alternative implementations are valid when they preserve the same ownership, snapshot, and publication guarantees.

## Relationship to Production Documentation

[System Architecture](../../architecture/architecture-overview.md) and [Transactional Execution](../../architecture/transactions.md) describe the supported steady-state runtime. The documents in this directory retain deeper implementation rationale, invariants, and subsystem boundaries that are useful when extending the transaction model without turning the production architecture documentation into an implementation inventory.
