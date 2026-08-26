# Transactional Environment Topology

> **Status:** Internal implementation design for the Stage 4 environment graph.

## Responsibility

The runtime environment subsystem is represented by one thin composite transaction participant because binding lifetime depends on the reconciled environment graph. The participant composes two focused state components rather than treating all environment state as one implementation concern:

```text
environment participant
  logical global environment id
  graph component
    nodes:         EnvironmentId -> EnvironmentNode
    registrations: name -> EnvironmentId
  binding component
    bindings:      (EnvironmentId, variable path) -> value
```

Graph state owns existence, inheritance, named visibility, and transient reachability. Binding state owns variable reads, writes, removals, and binding conflict keys. Both use persistent transaction-local baseline/change semantics.

The composite boundary exists because reconciliation is ordered: graph preparation determines the logical environment identities retained by the candidate, and binding preparation must omit changes for identities pruned by that graph candidate. Aggregate transaction atomicity alone would not justify combining the concerns; the transaction coordinator already provides atomic publication across independent participants.

## Logical Environment Nodes

A logical environment has a stable `RuntimeEnvironmentId` independent of the transaction and of any runtime view currently exposing it. Bound namespace views and transaction views retain that identity.

A node contains durable environment metadata:

- user-visible name;
- transient/lifetime status;
- optional inherited-parent relationship.

The inherited-parent relationship identifies the logical parent and preserves the parent view metadata that affects existing fallback semantics. In particular, the parent's bound namespace and override-resolution tags are captured with the relationship. Reconstructing a named inherited environment therefore does not silently change `@`, override, or inherited-resolution behavior merely because the original environment object no longer exists.

## Transaction-Bound Views

`IRuntimeEnvironment` remains a module-facing view rather than a state owner. Resolving or rebinding a logical environment constructs a view from the current transaction's environment component state.

A view carries the selected logical environment identity plus view-local metadata such as its current namespace and override tags. Variable access delegates to the binding map for that identity. Inherited lookup reconstructs the parent view from the topology stored in the same transaction.

Object identity is not part of the environment contract. Two resolved views of one logical environment may be different objects while observing the same transaction-local state.

Runtime services are not part of environment transaction state. Services needed by environment behavior, such as tagged-string conversion observation, are resolved from DI by the runtime and attached to materialized views. They are not persisted in logical nodes, fork baselines, or transaction change sets.

## Environment Creation

Creating a new environment allocates a new logical identity and records its node in the current transaction.

Scope semantics map onto topology as follows:

| Scope | Topology operation |
|---|---|
| `Isolated` | Create a node without a parent. |
| `InheritParent` | Create a node whose parent relationship targets the caller's current environment view. |
| `InheritGlobal` | Create a node whose parent relationship targets the root execution's logical global environment. |
| `Global` | Reuse the logical global identity. |
| `Parent` | Reuse the caller's parent environment identity as a view. |
| `Current` | Reuse the caller's current environment identity as a view. |
| `Reference` | Resolve a name through the transaction-local registration map. |

A non-transient named environment is added to topology and the registration map as one component operation. If the name already exists, the operation fails without adding a replacement node or changing the existing registration.

Transient environments are not registered by name. Explicitly named transient environments remain discoverable through the runtime ancestry where existing scope semantics permit that, but they do not become catalog entries.

## Named Registration Isolation

Named registrations are ordinary transaction-local changes. A child can immediately resolve a name it creates, while siblings and the owner continuation retain the fork-time registration baseline until reconciliation.

Two contributors registering the same logical name conflict under the default merge strategy even if their final node metadata would happen to be equal. The transaction coordinator publishes no environment candidate when that conflict is unresolved.

When a conflict strategy deliberately selects one contributor, topology belonging only to losing registrations is subject to normal reachability pruning before publication.

## Transient Reachability

Transient nodes created only for a child invocation must not accumulate in ancestor state merely because they existed during execution.

At reconciliation, the environment participant retains:

1. every node that already existed in the owner's fork baseline;
2. every environment targeted by the candidate named-registration map;
3. every ancestor required by those surviving environments.

New child-local nodes outside that reachable set are omitted from the candidate. Binding changes whose environment identities are omitted are discarded at the same time.

This rule preserves required inheritance chains. A named environment may inherit through an otherwise transient ancestor; that ancestor remains internally reachable even though it has no registration of its own.

Reachability is deliberately an environment-component rule rather than a generic transaction rule. Other transactional participants need not share environment lifetime semantics.

## Bindings and Topology Reconcile Together

Variable conflicts continue to use `(EnvironmentId, variable path)` as their logical key. Registration conflicts use the user-visible environment name, while topology conflicts use logical environment identity.

The graph fork prepares candidate node and registration maps first and computes the retained logical environment identities. The binding fork then prepares its candidate using that retained set. A thin environment fork composes the two detached candidates into one participant state before returning it to the transaction coordinator. No live environment state is mutated during preparation.

This provides two levels of atomicity:

- environment-local consistency across topology, registration, and bindings;
- aggregate transaction atomicity across the environment participant and every other transactional component.

## Root State

A root execution seeds one logical global environment node and its initial bindings. The root's environment component is otherwise ordinary transaction state; there is no mutable environment catalog or process-global binding store underneath it.

Environments created directly by the root execution remain part of that root's state. Child-local transient environments are pruned when they reconcile upward unless surviving topology requires them.

## Remaining Workflow-State Migration

The runtime named-module registry remains the next Cyborg-owned mutable workflow-state boundary. It should become a separate transactional participant because its values and conflict semantics differ from environment graph state, while the transaction coordinator still publishes both participants atomically.
