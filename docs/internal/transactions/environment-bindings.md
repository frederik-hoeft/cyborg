# Transactional Environment Bindings

> **Status:** Internal architecture notes for transactional environment bindings.

## Responsibility

Environment variable bindings are owned by a focused binding-state component inside the composite runtime-environment transaction participant. The binding component provides transactional reads, writes, removals, fork isolation, recursive reconciliation, and write-conflict semantics without making `RuntimeEnvironment` objects mutable state owners.

Topology, named registration, and transient lifetime are owned by a separate graph-state component described in [Transactional Environment Topology](environment-topology.md). The environment participant is composite only because the graph candidate determines which logical environment identities the binding candidate may retain.

## Logical Environment Identity

Each runtime environment state has a stable internal identity. Namespace binding and other lightweight environment views preserve that identity because they are different views of the same logical environment state.

A variable conflict key is therefore:

```text
(RuntimeEnvironmentId, variable name)
```

Two branches modifying the same variable name in different logical environments do not conflict. Two views of the same logical environment do conflict on the same variable binding.

The identity is independent of the transaction that currently views the environment. Rebinding an environment into a child transaction retains the identity while changing the participant state through which its variables are resolved.

## Binding State

The binding-state component contains a transactional dictionary keyed by logical environment identity and variable name.

```text
environment participant
  baseline: persistent map<(environment id, name), value>
  changes:  (environment id, name) -> Set(value) | Remove
```

This keeps the generic transaction coordinator unaware of environment semantics while reusing the persistent baseline/change behavior established by the transactional dictionary.

Removals remain explicit tombstones in the transaction-local change set. A binding that is set and then removed therefore still participates in later conflict detection even when the effective environment again matches its baseline.

## Transaction-Bound Views

`RuntimeEnvironment` remains the consumer-facing resolution/interpolation object, but its variable store is a transaction-bound facade rather than a mutable dictionary once the environment belongs to an execution session.

A view retains:

- logical environment identity;
- resolution/interpolation behavior;
- namespace and override-tag metadata;
- inherited-parent relationships;
- the transaction whose environment participant state is currently authoritative.

Forking does not copy an environment dictionary. A child runtime rebinds the relevant environment views to the child transaction. All branches then resolve the same logical environment identities through different transaction-local participant states derived from one immutable fork baseline.

Inherited environment views recursively rebind their parent views to the same transaction. A child therefore cannot accidentally fall through to a live parent-transaction variable store.

## Module Invocation Integration

Opening a module invocation now also opens a one-child transaction fork:

```text
caller transaction
  +-- continuation (empty for awaited child execution)
  +-- child invocation transaction
        +-- fresh DI scope
        +-- transaction-bound environment views
        +-- worker execution
```

The child transaction completes after the module lifecycle completes. Its binding changes are prepared and reconciled into the caller before the runtime resumes the caller. A failed or canceled module result still reconciles state under the current invocation policy; result status does not imply rollback.

Nested module execution repeats the same operation. Changes reconciled from an earlier nested child therefore remain part of the owning transaction's durable parent-relative change set and propagate when that transaction later joins its own parent.

## Artifact Publication

Artifact values are ordinary environment writes. Publication must therefore resolve its target through the current transaction rather than invoking a parent runtime whose transaction is currently frozen behind the child fork.

`Parent` artifact scope resolves to the logical parent environment view inside the current transaction. `Current`, `Global`, and named-reference targets likewise resolve through the transaction-bound runtime environment context.

This removes artifact publication as a pre-join visibility escape path for variable bindings. Named-reference resolution uses the transaction-local topology described in [Transactional Environment Topology](environment-topology.md).

## Root State

A root execution owns a parentless transaction containing the environment participant. Variables supplied before `RootModuleRuntime` construction can seed the root participant baseline. Variables applied to `runtime.GlobalEnvironment` after session creation are ordinary root-local changes.

There is no mutable process-global binding dictionary underneath the transaction tree. The root's effective state is its immutable baseline plus its own durable changes.

## Environment Graph Integration

Bindings and graph state reconcile as focused component candidates and are then composed into one environment participant candidate. See [Transactional Environment Topology](environment-topology.md) for creation, registration conflicts, inheritance edges, pruning semantics, and the graph-before-bindings reconciliation dependency.
