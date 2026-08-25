# Transactional Environment Bindings

> **Status:** Internal implementation design for the first Stage 4 environment-state slice. Environment catalog/topology migration remains separate.

## Responsibility

Environment variable bindings are the first Cyborg-owned workflow state carried by the generic transaction coordinator. The binding layer provides transactional reads, writes, removals, fork isolation, recursive reconciliation, and write-conflict semantics without making `RuntimeEnvironment` objects mutable state owners.

This slice intentionally separates **binding state** from **environment topology**. Existing environment creation and named registration still use the runtime catalog until the topology migration described in [Environment and Runtime State](environment-and-runtime-state.md) is implemented.

## Logical Environment Identity

Each runtime environment state has a stable internal identity. Namespace binding and other lightweight environment views preserve that identity because they are different views of the same logical environment state.

A variable conflict key is therefore:

```text
(RuntimeEnvironmentId, variable name)
```

Two branches modifying the same variable name in different logical environments do not conflict. Two views of the same logical environment do conflict on the same variable binding.

The identity is independent of the transaction that currently views the environment. Rebinding an environment into a child transaction retains the identity while changing the participant state through which its variables are resolved.

## Binding Participant

One transaction participant owns all environment variable bindings for an execution tree. Its state is a transactional dictionary keyed by logical environment identity and variable name.

```text
environment-binding participant
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
- the transaction whose environment-binding participant state is currently authoritative.

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

The child transaction completes after the module lifecycle completes. Its binding changes are prepared and reconciled into the caller before the runtime resumes the caller. A failed/canceled module result still reconciles state in this migration stage; result-status-driven rollback remains a later policy.

Nested module execution repeats the same operation. Changes reconciled from an earlier nested child therefore remain part of the owning transaction's durable parent-relative change set and propagate when that transaction later joins its own parent.

## Artifact Publication

Artifact values are ordinary environment writes. Publication must therefore resolve its target through the current transaction rather than invoking a parent runtime whose transaction is currently frozen behind the child fork.

`Parent` artifact scope resolves to the logical parent environment view inside the current transaction. `Current`, `Global`, and named-reference targets likewise resolve through the transaction-bound runtime environment context.

This removes artifact publication as a pre-join visibility escape path for variable bindings. Named-reference **registration/topology** is still pending the next environment-state slice.

## Root State

A root execution owns a parentless transaction containing the environment-binding participant. Variables supplied before `RootModuleRuntime` construction can seed the root participant baseline. Variables applied to `runtime.GlobalEnvironment` after session creation are ordinary root-local changes.

There is no mutable process-global binding dictionary underneath the transaction tree. The root's effective state is its immutable baseline plus its own durable changes.

## Remaining Environment Migration

The following state is deliberately not solved by this slice:

- named environment registration/removal;
- environment topology and parent identity;
- transaction-local environment creation/catalog visibility;
- transient environment reachability and pruning.

Until those move into the environment transactional component, a child can still affect catalog/topology visibility even though the variables stored in an existing logical environment are transaction-isolated. The next environment-state task should migrate those concerns using the same `RuntimeEnvironmentId` identity rather than adding a second environment identity system.
