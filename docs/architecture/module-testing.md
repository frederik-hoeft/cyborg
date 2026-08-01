# Module Testing Architecture

This document describes the reusable module-test adapter shared by `Cyborg.Modules.Tests` and `Cyborg.Modules.Borg.Tests`. The adapter runs tests against the production deserialization, dependency-injection, runtime-environment, validation, and execution infrastructure while keeping individual test cases focused on assertions.

For the runtime lifecycle exercised by these tests, see [Architecture Overview](architecture-overview.md). For generated validation behavior, see [Source Generators](source-generators.md) and [Validation Attributes Reference](validation-attributes-reference.md).

## Components

### `CyborgTestBase`

`CyborgTestBase` is the higher-order-function façade used by module tests. Each helper creates an isolated service provider and `TestModuleRuntimeScope`, runs the requested production pipeline, invokes the supplied assertion, and disposes the scope.

The base class registers `ICyborgCoreServices`. Domain-specific subclasses extend `ConfigureServices`:

| Test base | Additional Jab module registrations |
|---|---|
| `ModuleTestBase` | `ICyborgModuleServices` |
| `BorgModuleTestBase` | `ICyborgModuleServices`, `ICyborgBorgServices` |

### `JabServiceDiscovery`

Production uses Jab's compile-time service graph. Tests use Microsoft.Extensions.DependencyInjection, so `JabServiceDiscovery` reflects over Jab service-module attributes and translates singleton, scoped, transient, import, and factory registrations into an `IServiceCollection`.

Jab emits its attribute types into each consuming assembly. Discovery therefore matches the generated attributes by namespace and metadata name rather than CLR type identity.

### `TestModuleRuntimeScope`

`TestModuleRuntimeScope` owns the isolated runtime state for one test invocation:

- a dedicated `ServiceProvider`;
- a fresh `GlobalRuntimeEnvironment` and root module runtime;
- production module and module-context deserialization;
- execution helpers for workers and module contexts;
- deterministic asynchronous disposal.

### `TestServiceConfiguration`

`TestServiceConfiguration.CreateDefaultServices()` creates the mutable service collection. `CyborgTestBase.ConfigureServices` and optional per-test callbacks populate it before the scope is created.

## Higher-Order Test APIs

### Deserialization

`TestDeserializationAsync<TModule>` deserializes a module reference through the production loader registry and passes the typed module record to the assertion.

`TestContextDeserializationAsync` performs the equivalent operation for a complete `ModuleContext` envelope.

Use these helpers for polymorphic dispatch, JSON shape, custom converter, and default serializer behavior. Deserialization failures such as malformed `DynamicKeyValuePair` objects surface as `JsonException` before module validation.

### Validation

`TestValidationAsync<TModule>` deserializes a module and executes its complete generated preparation and validation pipeline:

1. apply defaults;
2. resolve overrides;
3. reapply defaults;
4. interpolate eligible strings;
5. validate constraints.

The assertion receives `ValidationResult<TModule>`. Invalid results contain errors and have no module instance; valid results contain the fully transformed module.

`TestValidatedModuleAsync<TModule>` additionally calls `EnsureValid()` and passes the validated module plus runtime scope to the assertion.

Test-only generated module records may also be instantiated directly when a regression concerns a state that cannot be represented faithfully in JSON, such as `default(ImmutableArray<T>)` versus `ImmutableArray<T>.Empty`. Such tests should create an isolated `TestModuleRuntimeScope` through the same service-configuration chain and call the generated `ValidateAsync` method directly.

### Overrides and interpolation

`TestOverridesAsync<TModule>` configures the global environment before validation and passes the valid transformed module to the assertion. It covers both typed property override resolution and the later string-interpolation phase.

Override resolution and interpolation are separate contracts:

- `[IgnoreOverride]` controls environment-driven property replacement;
- `[IgnoreInterpolation]` preserves a string for later context-specific interpolation;
- `ModuleBase.Name` and `ModuleBase.Group` opt out of both because runtime environment binding consumes them before validation.

### Execution

`TestModuleAsync<TModule, TWorker>` executes a deserialized module and passes the module, typed worker, and `IModuleExecutionResult` to the assertion.

`TestExecutionAsync` is the simplified result-only form. `TestExecutionThrowsAsync<TException>` verifies expected execution failures. `TestModuleContextAsync` executes a complete context with environment setup, configuration, and requirements.

## Service customization

Override `ConfigureServices(IServiceCollection, IJabServiceDiscovery)` for test-class-wide registrations and call the base implementation first. Every HOF also accepts a per-test `configureServices` callback, applied after the class-level registrations.

Per-test registrations can replace production services with fakes or deterministic implementations. A fresh provider is built for every invocation, so registrations and runtime state do not leak between tests.

## JSON source resolution

Every JSON-based HOF accepts an explicit JSON string. When it is null, the helper calls `GetDefaultModuleJsonAsync()`, which a test class may override to load a shared fixture.

Explicit JSON always takes precedence over the fallback.

## Isolation

Each HOF invocation creates and disposes an independent service provider, root runtime, global environment, loader context, and module registry. Tests can therefore run in parallel without sharing environment variables, scoped services, or runtime artifacts.

## Regression-test guidance

Generated-pipeline regressions should assert observable contracts rather than generated source formatting. In particular:

- distinguish a default `ImmutableArray<T>` from an initialized empty array;
- verify invalid collection states produce `ValidationError` entries rather than enumeration exceptions;
- cover nullable value-type collections and nullable elements separately;
- verify collection-element defaults run before interpolation;
- verify `[IgnoreInterpolation]` strings remain raw;
- verify structural `Name` and `Group` values remain unchanged;
- verify malformed dynamic key/value JSON fails during deserialization;
- verify values intentionally interpolated after child execution are not resolved prematurely.

Compile-time diagnostic contracts that cannot coexist with a successful test-project build belong in focused generator-driver tests rather than runtime module tests.
