# State and Reconciliation

> **Status:** Internal target design. See [Transactional Execution Design](README.md) for scope and invariants.

## Responsibility

The transaction subsystem owns workflow-state ancestry and reconciliation. It does not know module-specific control flow and does not use DI scopes as a state container. A transaction node represents one invocation's isolated view of all participating workflow-semantic state.

The central model is deliberately recursive:

```text
transaction effective state = immutable baseline + local changes
```

A child's baseline is the parent's effective state captured at fork time. The parent effective state is itself its own baseline plus changes. The recursion ends at an immutable execution seed for a root transaction.

## There Is No Global Commit Store

Transactional state is represented recursively as an immutable fork-time baseline plus a transaction-local change set. There is no mutable canonical state beneath the transaction tree. Joining reconciles a child's changes into its parent's change set; it does not commit to an external or global backing store. Change entries may be compacted to their final operation, but explicit modification provenance is preserved until the transaction itself terminates.

A root transaction has no parent to receive its changes. Finishing a root therefore closes an execution rather than committing to a hidden process-global state object.

## Baseline and Change Ownership

Each transactional component in a node has two logical inputs:

```text
Baseline
  immutable effective component state captured at node creation

Changes
  operations explicitly performed by this node or reconciled from descendants
  relative to Baseline
```

Only the local change set is mutable transaction-owned state. The baseline is immutable and structurally shareable.

For map-like state, a change is conceptually one of:

```text
Set(value)
Remove
```

Presence of a key in the change set means the key was modified. The transaction never removes provenance merely because the final effective state compares equal to the baseline.

Examples:

```text
baseline: x = 1
set x = 2
set x = 1

final visible value: x = 1
change provenance:   x -> Set(1)
```

```text
baseline: x absent
set x = 1
remove x

final visible value: x absent
change provenance:   x -> Remove
```

This keeps conflict detection based on explicit writes rather than arbitrary value equality or object identity.

## Persistent State and Structural Sharing

Forking must not deep-copy every dictionary or graph. Transactional components should use immutable/persistent effective snapshots with structural sharing and small local change sets.

For map-like state, the intended abstraction is conceptually:

```text
transactional map
  immutable persistent baseline
  local change map: key -> Set(value) | Remove
  optional cached/frozen effective snapshot derived from baseline + changes
```

Reads consult local changes first and fall back to the immutable baseline. A fork needs a stable effective snapshot, so it applies the current local changes to the persistent baseline and freezes the result. Persistent updates copy only the affected structural paths and share unchanged nodes with previous snapshots.

The implementation may cache a derived effective snapshot and invalidate it on local writes. The cache is derived state, not a second mutable source of truth. Baseline and explicit changes remain authoritative.

A mutable builder/transient facade can be used internally while materializing an immutable snapshot, provided an already-published snapshot can never be modified through that builder. `ImmutableDictionary<TKey, TValue>` and its builder are reasonable initial candidates; a custom persistent trie is justified only by measured need.

### Why the change set remains separate

A persistent effective map alone cannot express the required provenance:

```text
baseline: x absent
set x
remove x
```

The effective map is identical to the baseline, but the transaction still changed `x`. Conflict detection therefore requires an explicit local change set even when effective state uses persistent collections.

## Fork Groups

A transaction node can open a structured fork group. The group captures one stable effective baseline for all contributors in that generation.

```text
owner durable state before fork
            |
            +-- immutable fork baseline S
                    +-- owner continuation branch
                    +-- child A transaction
                    +-- child B transaction
```

The owner durable state is not mutated while the fork group is open. Work after the fork belongs to the explicit continuation branch. This prevents post-fork owner writes from leaking into child baselines and gives reconciliation a complete set of contributors.

Ordinary awaited child execution has an empty continuation because the owner is suspended. `Parallel` also has an empty continuation initially. Future managed background work can execute the owner continuation concurrently without changing the state model.

Nested fork groups belong to a branch and close in structured LIFO order. An owner cannot close a transaction while one of its fork groups remains open.

## Recursive Composition

When one child joins successfully, its changes are folded into a candidate change set for its owner. Those changes remain parent-relative changes of the owner.

For sequential execution:

```text
owner baseline
  + owner-local changes
  + child 1 joined changes
  + child 2 joined changes
  = owner effective state
```

Starting child 2 may require freezing a new effective snapshot, but doing so never clears the owner change set. This distinction between an immutable child baseline and durable owner provenance is fundamental.

The same rule holds at arbitrary depth: a grandchild change reconciled into a child remains dirty when that child later reconciles into its parent.

A child can also simplify the parent's final operation without erasing provenance. For example:

```text
grandparent baseline: x absent
parent changes:       x -> Set(1)
child baseline:       x = 1
child changes:        x -> Remove

child joins parent:
parent visible state: x absent
parent changes:       x -> Remove
```

The child removal does not conflict with the earlier parent write because that write is part of the stable fork baseline, not a concurrent contributor. The resulting `Remove` remains dirty relative to the grandparent even though the final visible state is again absence.

## Transactional Components

Workflow state is split into independently typed transactional components. The initial built-in components are environment state and runtime named-module state. Runtime/custom services can add further components through the participation model described in [Transactional Services](transactional-services.md).

A component is responsible for the semantics of its state:

- constructing root state from a seed;
- exposing an immutable effective snapshot suitable for fork;
- recording local changes;
- defining logical conflict keys;
- preparing reconciliation of continuation and child changes;
- producing candidate component state and candidate parent-relative provenance.

The transaction coordinator owns topology and publication. Components do not independently mutate parent state during join.

## Two-Phase Reconciliation

A join has a preparation phase and one aggregate publication phase.

```text
PREPARE
  environment component -> candidate
  module registry        -> candidate
  custom component A     -> candidate
  custom component B     -> candidate

  any conflict/failure?
       yes -> discard all candidates
       no  -> publish

PUBLISH
  install one resulting aggregate execution-state bundle
```

Preparation must be side-effect-free with respect to parent-visible transactional state. A candidate may share immutable storage with the parent, but the parent's currently visible component bundle cannot be modified.

Publication replaces one aggregate state reference or equivalent atomic owner-state handle. Existing scoped facades resolve through that handle so they observe the completed join without replacing the parent DI scope.

If any component cannot reconcile, the pre-fork owner state remains unchanged by the entire fork generation.

## Conflict Semantics

Each component defines logical conflict keys. The initial merge strategy fails when more than one fork contributor explicitly changes the same logical key.

For a map-like component:

```text
A: Set(x)
B: Set(x)      -> conflict

A: Set(x)
B: Remove(x)   -> conflict

A: Remove(x)
B: Remove(x)   -> conflict
```

Final value equality does not remove the conflict. A contributor that writes and later restores the baseline still counts as a writer.

Reads are not tracked for conflict detection. The first migration therefore provides deterministic snapshot isolation with write/write conflict detection, not serializable database semantics.

### Owner continuation conflicts

The owner continuation is a peer contributor for the open fork generation. If both the continuation and a child modify the same logical key, the default strategy reports the same conflict as two sibling children.

### Extensibility

The transaction coordinator should depend on an explicit conflict-strategy abstraction rather than hard-code failure forever. The first implementation needs only deterministic fail-on-conflict semantics, but component preparation should be capable of delegating conflict decisions without changing transaction topology.

The strategy is not allowed to break atomicity. It selects a candidate result; publication still occurs only after every component has prepared successfully.

## Change Compaction

A component can compact multiple operations to the final operation for a logical key without retaining a temporal operation log:

```text
Set(1), Set(2), Set(3) -> Set(3)
Set(1), Remove          -> Remove
Remove, Set(2)          -> Set(2)
```

Compaction changes representation, not provenance. Once a key has been successfully changed in the transaction, it remains in the change set until the transaction terminates.

This is sufficient for the initial write/write conflict model and avoids maintaining unnecessary operation history.

## Transaction Lifecycle

A transaction moves through a small number of semantic states:

```text
active
  -> fork group open
  -> active with reconciled changes
  -> completed / discarded
```

A completed or discarded transaction cannot accept further writes, open forks, or reconcile a second time. Child ownership is explicit so duplicate joins and use-after-disposal become lifecycle errors rather than silently mutating state twice.

## State Outside the Transaction Model

Transactions do not protect:

- external process/network/filesystem side effects;
- static mutable state;
- arbitrary singleton internals;
- mutable object state reached through a value stored in an environment binding.

Those boundaries are intentional. A service should become transaction-aware only when its state represents workflow semantics that must inherit, isolate, and reconcile with module execution.
