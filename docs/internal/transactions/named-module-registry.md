# Transactional Named-Module Registry

> **Status:** Internal implemented design. See [Transactional Execution Design](README.md) for the surrounding transaction model.

## Responsibility

The named-module registry is workflow state that maps a logical module name to an immutable loaded `ModuleContext`. It is a separate transaction participant from the environment subsystem because its domain state and conflict keys are independent. Cross-component atomicity is supplied by the transaction coordinator rather than by coupling registry and environment implementations.

The registry has two input paths:

- configuration loading discovers static named definitions and produces immutable seed data;
- executing modules can register or remove names through the current invocation's scoped `IModuleRegistry` facade.

Neither path mutates process-global workflow state.

## Configuration-Load Seeds

Named definitions are discovered recursively while one configuration graph is deserialized. Discovery belongs to that load operation, not to the application service provider or a runtime singleton.

A load therefore owns a temporary seed builder:

```text
configuration load
  +-- deserialize root context
  |     +-- nested module context "build"
  |     +-- nested module context "cleanup"
  |
  +-- immutable seed
        "build"   -> loaded ModuleContext
        "cleanup" -> loaded ModuleContext
```

The resulting immutable seed is attached to the loaded root `ModuleContext`. Loaded registry values contain immutable module definitions and activation identity; they never contain worker instances.

The ordinary singleton JSON converter does not hold registry state. The configuration loader creates a load-local serializer view with a seed-collecting module-context converter, and nested module/dynamic-value deserialization uses that same serializer context. This keeps recursive discovery coherent without ambient or process-global state.

Duplicate names within one load retain the existing first-registration-wins behavior. The seed records the first definition and ignores later duplicates.

## Seed Application

Entering a `ModuleContext` applies its immutable seed to the **current transaction** before requirement import, optional configuration execution, and main-module execution. Seed application is therefore an ordinary transactional registry modification rather than a special root-only initialization step.

This matters for dynamically loaded configuration. An `External`, template, switch, or similar configuration consumer can load another graph during execution; when that loaded root context executes, its seed is applied to that nested transaction. The definitions are immediately visible to work inside that transaction and reconcile normally into its parent.

Applying a seed uses normal add semantics. A name already visible in the current transaction is retained rather than replaced implicitly.

## Transaction State

The registry participant owns one transactional dictionary:

```text
module name -> ModuleContext
```

Its logical conflict key is the module name. Local operations are explicit additions/replacements or removals, with the same durable change provenance used by other transactional dictionaries.

Forks share an immutable baseline. Registrations and removals are branch-local until reconciliation. Two contributors that change the same name conflict under the default strategy, including equal final values or two removals, because conflict detection is based on explicit modification provenance rather than value equality.

Nested joins preserve registry changes as parent-relative changes so later ancestor reconciliation cannot lose earlier registrations or removals.

## Scoped Runtime Facade

`IModuleRegistry` remains the service consumed by module workers. Its default implementation is scoped rather than application-singleton. When an invocation DI scope is created, runtime orchestration binds that scoped facade to the registry participant state for the invocation transaction.

Consequently:

- worker constructor injection resolves an invocation-local registry facade;
- reads and writes automatically target the current transaction;
- modules do not inject the transaction coordinator or participant state directly;
- absence of the scoped runtime binding is an invalid use of the default registry rather than a fallback to process-global state.

The runtime-internal registry bridge owns participant identity and binding of scoped facades. It is not a module-facing service.

## Root Ownership

Each root runtime creates its own registry participant together with the rest of its transaction coordinator. Multiple root runtimes resolved from the same application provider therefore share ordinary application singletons but do not share named-module workflow state.

Successful child changes reconcile into the root transaction and remain available to later module invocations on that same root. A different root starts from independent registry state unless its executed configuration explicitly seeds the same definitions.

## Atomicity with Other Runtime State

Registry and environment state remain separate participants. The transaction coordinator prepares candidates for both before publishing either.

If an environment candidate prepares successfully but a named-module registration conflicts, the owner transaction publishes neither candidate. Registry isolation therefore does not require environment-specific code, and future transactional services can participate under the same aggregate publication rule.
