# Runtime Responsibility Boundaries

> **Status:** Internal runtime composition design. This document describes the responsibility split used by the transactional runtime architecture in this directory.

## Purpose

The runtime is the consumer-facing execution context for module workers. It should remain the single facade through which workers access their current environment, execute nested modules, prepare child environments, and complete execution. Those capabilities do not require the facade itself to own every implementation mechanism.

The runtime has several independent responsibilities that meet at `IModuleRuntime` but should not be implemented by one monolithic runtime object. Keeping execution orchestration, environment views, artifact publication, and runtime service composition behind focused internal boundaries lets the public facade remain stable while transaction state and execution scopes evolve independently.

## Consumer-Facing Runtime

`IModuleRuntime` remains the module-facing API. Module workers should not need to inject low-level execution, environment-catalog, transaction, or artifact-publication services for ordinary work.

The runtime facade owns the behavioral contract, not the backing mechanisms. Its core responsibilities are:

- expose the current, parent, and logical-global environment views;
- execute a `ModuleContext` in an explicitly selected environment;
- execute a loaded `ModuleReference` in an explicitly selected environment;
- prepare an environment from a `ModuleEnvironment` description;
- resolve configured environment references;
- complete a module result and publish its artifacts according to module policy.

Overloads that only choose a default environment or translate `EnvironmentScope`/`ModuleContext` metadata into one of those core operations are convenience syntax and belong in runtime extension methods. Environment catalog mutation is runtime infrastructure and is not part of the consumer contract.

Every invocation owns a DI scope and transaction node, while the public facade remains stable over that execution/session state.

Internal runtime collaborators should depend on narrow internal capability interfaces rather than concrete runtime implementations when they need behavior beyond `IModuleRuntime`. Concrete runtime types should only be coupled directly where they form an intentionally inseparable implementation pair, such as a private or internal scope-bound runtime created by the runtime facade itself.

## Environment Context and Graph

The runtime-relative environment context owns current, parent, and logical-global view relationships plus environment creation/reference resolution. Durable workflow state underneath those views belongs to the transaction-owned environment graph rather than to runtime objects.

The environment graph owns logical environment identity, inheritance topology, named registration, transient lifetime, and variable bindings as one transactional component. The runtime context translates module-facing scope operations into graph/view operations; it does not own a separate mutable catalog.

The runtime environment context remains intentionally internal. Modules continue to use `IModuleRuntime` and `IRuntimeEnvironment`; they do not consume environment graph or transaction services directly.

## Module Context Execution

Executing a `ModuleContext` is orchestration above raw worker dispatch. It consists of:

1. resolving and importing required arguments into the selected environment;
2. executing the optional configuration module in that environment;
3. stopping when configuration fails or is canceled;
4. executing the main loaded module reference.

This is one cohesive responsibility and should be isolated from raw worker dispatch. It is also the boundary that later owns the main invocation transaction while configuration execution becomes a nested child invocation.

## Worker Dispatch and Lifecycle

Raw worker dispatch owns execution mechanics after a module definition has already been selected and an environment has already been prepared:

- activate a fresh worker from the current execution service provider;
- invoke the worker against its scoped runtime;
- translate cancellation and unhandled exceptions into execution results;
- emit lifecycle logging;
- run post-execution hooks without allowing hook failure to replace the module result.

This mechanism is internal runtime infrastructure. It resolves scope-sensitive services from the current invocation provider; module consumers do not need direct access to it.

Keeping activation/dispatch separate from `ModuleContext` orchestration also keeps the scope boundary explicit: the invocation scope exists before worker activation and remains valid through post-execution hooks.

## Artifact Publication

`Exit` is part of the module-facing runtime contract because it converts a worker result into the externally visible completed result and applies configured artifact-publication semantics. The publication mechanics are nevertheless a distinct policy responsibility.

Artifact publication determines the responsible runtime-relative environment target, builds the artifact collection, publishes it, and returns the normalized execution result. Publication resolves transaction-bound environment views, so artifact writes participate in the current transaction without changing worker APIs.

## Runtime Service Composition and Assisted Construction

Runtime objects combine two different kinds of inputs that should not be propagated in the same way:

- context-free services such as syntax policy, diagnostics, logging, and observers belong to dependency injection;
- execution-specific values such as namespace, logical environment identity, parent view, and transaction belong to the runtime operation that creates the object.

Environment and artifact views therefore use an internal assisted-construction factory. DI supplies the context-free services once, while callers provide only the contextual values needed for the requested view. A runtime environment must not copy DI services from another environment merely because that environment happens to be available as a construction source. This keeps transaction snapshots and logical environment graph state free of service references.

`VariableSyntaxBuilder` is a context-free naming/syntax policy and is shared as a singleton DI service. It has no environment-specific mutable state, so introducing another interface solely to hide its concrete type would add indirection without creating a meaningful extension boundary. Runtime views and artifact collections created through the assisted factory use that DI-owned instance.

Stateless runtime mechanisms such as module-context orchestration, raw worker dispatch, and artifact publication are separated behind narrow internal capability interfaces. Runtime nodes carry an internal operations bundle rather than constructing or depending directly on those concrete mechanisms. `RootModuleRuntime` and `ScopedRuntime` remain intentionally concrete companions because they represent contextual execution nodes whose construction is owned by the runtime implementation itself.

Jab generates imported service-module providers in the consuming assembly. Registering internal Core implementation types directly would therefore expose inaccessible types to generated host code. Internal runtime mechanisms stay internal and are composed at the Core DI factory boundary from DI-provided public services instead of being made public merely to satisfy container generation. This composition step is the only place that should manually connect those context-free services to internal runtime factories and mechanisms.

## Runtime Environment Internals

`EnvironmentLike` and `RuntimeEnvironment` contain two layers that should be kept conceptually separate:

- **environment state storage**: bindings and later transaction-local changes;
- **environment semantics**: resolution, interpolation, indirection, overrides, decomposition, namespaces, and inherited lookup.

Bindings are backed by transaction-owned persistent baseline/change state rather than by a mutable canonical dictionary. Runtime environment instances are behavioral views over logical environment identity and transaction state, so multiple CLR objects may represent the same logical environment without duplicating its state.

Module namespace calculation and bulk publication are convenience operations rather than intrinsic mutable environment state. They can remain extension-level behavior while the core `IRuntimeEnvironment` contract focuses on resolution, binding, override state, and artifact/decomposition semantics.

## What Should Not Become Consumer DI

The refactor deliberately does **not** introduce module-facing services such as `IModuleExecutor`, `IEnvironmentCatalog`, `IArtifactPublisher`, or `ITransactionManager` that workers must inject for normal execution.

Low-level runtime mechanisms may later be represented as internal DI services where lifetime or testability benefits from it, but ordinary modules should continue to receive one `IModuleRuntime` execution facade. Explicit transactional participation for custom DI services is a separate opt-in extension point described in [Transactional Services](transactional-services.md); it is not the basic runtime API.

## Transaction Integration

The responsibility split aligns directly with the transactional runtime:

- per-invocation DI scope creation wraps worker activation/dispatch without changing module-context semantics;
- runtime environment contexts materialize views over transaction-owned environment graph state;
- named registration, environment topology, and bindings remain transaction-owned component state rather than runtime-owned mutable state;
- artifact publication produces ordinary transactional environment changes;
- `ModuleContext` orchestration owns the main invocation transaction while configuration executes as a nested invocation;
- `IModuleRuntime` remains a narrow facade over execution-session-local state.

Future transactional participants should follow the same pattern: state and reconciliation remain subsystem-owned, while runtime orchestration depends only on narrow capabilities required to coordinate execution.
