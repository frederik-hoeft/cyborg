# Module Testing Architecture

This document describes the design and architecture of the reusable unit testing adapter. The adapter is split across two layers: `Cyborg.Core.TestAdapter` (a shared library providing the base class and DI infrastructure) and per-domain test projects (`Cyborg.Modules.Tests`, `Cyborg.Modules.Borg.Tests`) that extend it with domain-specific service registrations.

**Table of Contents**

<!-- @import "[TOC]" {cmd="toc" depthFrom=2 depthTo=6 orderedList=false} -->

<!-- code_chunk_output -->

- [Overview](#overview)
- [Component Architecture](#component-architecture)
  - [Component Diagram](#component-diagram)
  - [JabServiceDiscovery](#jabservicediscovery)
  - [TestServiceConfiguration](#testserviceconfiguration)
  - [TestModuleRuntimeScope](#testmoduleruntimescope)
  - [CyborgTestBase](#cyborgtestbase)
  - [Per-Domain Test Base Classes](#per-domain-test-base-classes)
- [Higher-Order Function API](#higher-order-function-api)
  - [Deserialization Testing](#deserialization-testing)
  - [Validation Testing](#validation-testing)
  - [Override Testing](#override-testing)
  - [Module Execution Testing](#module-execution-testing)
  - [Exception Testing](#exception-testing)
  - [Full Module Context Testing](#full-module-context-testing)
- [Service Configuration Model](#service-configuration-model)
  - [Per-Test-Class Configuration](#per-test-class-configuration)
  - [Per-Test-Case Configuration](#per-test-case-configuration)
- [Module JSON Source Resolution](#module-json-source-resolution)
- [Isolation Model](#isolation-model)
- [Design Decisions](#design-decisions)
  - [Reflection-Based Jab Discovery](#reflection-based-jab-discovery)
  - [Facade Pattern Over Monolith](#facade-pattern-over-monolith)
  - [InternalsVisibleTo](#internalsvisibleto)

<!-- /code_chunk_output -->

## Overview

The testing adapter enables writing per-module unit tests that cover the following common scenarios:

1. **Polymorphic deserialization** — Verifying that module JSON is correctly deserialized into the expected module record type via the registry-based deserialization pipeline.
2. **Validation attribute enforcement** — Checking that required fields, regex patterns, range constraints, and other validation attributes produce expected validation results.
3. **Runtime override resolution** — Confirming that environment variable injection, property overrides, and default value application produce correctly transformed module records.
4. **Worker execution and artifact publishing** — Asserting actual vs. expected module results, exit statuses, artifact paths, and published environment variables after execution.
5. **Exception handling and error reporting** — Testing that execution produces expected exceptions or error states (validation failures, skipped/failed statuses).

The adapter is structured as a layered set of components, each with a single responsibility, composed behind the `CyborgTestBase` façade class in `Cyborg.Core.TestAdapter`.

## Component Architecture

### Component Diagram

```
┌──────────────────────────────────────────────────────────────────────────────────────┐
│  Per-Module Test Class (extends ModuleTestBase or BorgModuleTestBase)               │
│  ┌────────────────────────────────────────────────────────────────────────────────┐  │
│  │  TestMethod: calls HOF methods with module JSON + assertion                  │  │
│  └─────────────────────────────────────┬──────────────────────────────────────────┘  │
└────────────────────────────────────────┼─────────────────────────────────────────────┘
                                         │
         ┌───────────────────────────────┼──────────────────────────────┐
         │                               │                              │
┌────────▼──────────────┐    ┌───────────▼────────────────┐  ┌─────────▼────────────────┐
│ ModuleTestBase         │    │ BorgModuleTestBase          │  │ (future domain base)     │
│ (Cyborg.Modules.Tests) │    │ (Cyborg.Modules.Borg.Tests) │  │                          │
│                        │    │                             │  │                          │
│ + ICyborgModuleServices│    │ + ICyborgModuleServices     │  │ + domain-specific        │
│   via ConfigureServices│    │ + ICyborgBorgServices       │  │   Jab modules            │
└──────────┬─────────────┘    └─────────────┬───────────────┘  └─────────────┬────────────┘
           └─────────────────────────────────┴──────────────────────────────┬─┘
                                                                             │ extends
                                                          ┌──────────────────▼──────────────────┐
                                                          │         CyborgTestBase               │
                                                          │  (Cyborg.Core.TestAdapter)           │
                                                          │  (HOF façade, public API)            │
                                                          │                                      │
                                                          │  + ICyborgCoreServices               │
                                                          │  • TestDeserializationAsync          │
                                                          │  • TestValidationAsync               │
                                                          │  • TestOverridesAsync                │
                                                          │  • TestModuleAsync                   │
                                                          │  • TestExecutionAsync                │
                                                          │  • TestExecutionThrowsAsync          │
                                                          │  • TestModuleContextAsync            │
                                                          └────────┬──────────────┬──────────────┘
                                                                   │              │
                                          ┌────────────────────────▼──┐  ┌────────▼────────────────────┐
                                          │ TestServiceConfiguration   │  │ TestModuleRuntimeScope      │
                                          │                            │  │                             │
                                          │  CreateDefaultServices()   │  │  • Runtime + Environment    │
                                          │  returns empty             │  │  • DeserializeModule()      │
                                          │  ServiceCollection         │  │  • ExecuteAsync()           │
                                          └──────────┬─────────────────┘  │  • IAsyncDisposable         │
                                                     │                    └──────────┬──────────────────┘
                                          ┌──────────▼───────────┐                  │
                                          │ IJabServiceDiscovery  │                  │ uses
                                          │ JabServiceDiscovery   │       ┌──────────▼────────────────┐
                                          │                       │       │  Production runtime:      │
                                          │  Reflects over Jab    │       │  RootModuleRuntime,       │
                                          │  [Singleton<>],       │       │  GlobalRuntimeEnvironment │
                                          │  [Scoped<>],          │       │  IJsonLoaderContext,      │
                                          │  [Transient<>] and    │       │  IModuleLoaderRegistry    │
                                          │  [Import<>] attrs     │       └───────────────────────────┘
                                          │  to build MEDI        │
                                          │  registrations        │
                                          └───────────────────────┘
```

### JabServiceDiscovery

**File:** `Cyborg.Core.TestAdapter/JabServiceDiscovery.cs` (implements `IJabServiceDiscovery`)

Translates Jab's compile-time DI declarations into MEDI runtime registrations via reflection. Jab generates its attribute types as `internal` within each project, so the test adapter references Jab directly and uses `typeof(SingletonAttribute<>).Name` etc. to obtain stable, compiler-checked attribute names. Matching is done by name and namespace against `CustomAttributeData` (not by type identity, since each assembly gets its own internal copy).

Key behaviors:
- Recursively processes `[Import<TModule>]` references (depth-first) to capture the full service graph across module boundaries (`ICyborgCoreServices` → `IDynamicValueProviderServices`, `IConfigurationTrustServices`, etc.).
- Supports all three Jab service lifetimes: `[Singleton<T>]`, `[Scoped<T>]`, and `[Transient<T>]` (each in one- and two-type-argument forms).
- Handles factory-based registrations by invoking the static factory method on the declaring interface with parameters resolved from the service provider.
- Handles constructor-based registrations via `ActivatorUtilities.CreateInstance`.

### TestServiceConfiguration

**File:** `Cyborg.Core.TestAdapter/TestServiceConfiguration.cs`

Provides a single static factory method `CreateDefaultServices()` that returns a new empty `IServiceCollection`. The actual service registrations happen through the `ConfigureServices` override chain (see [Service Configuration Model](#service-configuration-model)) rather than here. The returned collection is mutable, enabling callers to add services before the service provider is built.

### TestModuleRuntimeScope

**File:** `Cyborg.Core.TestAdapter/TestModuleRuntimeScope.cs`

Encapsulates the per-test lifecycle: builds a `ServiceProvider` from the configured `IServiceCollection`, constructs a `RootModuleRuntime` with a fresh `GlobalRuntimeEnvironment`, and provides methods for module deserialization and execution. Implements `IAsyncDisposable` for deterministic cleanup.

Key methods:
- `Create(IServiceCollection)` — Static factory that builds the scope from a configured service collection.
- `DeserializeModule(string)` — Runs a module JSON string through the production `ModuleReferenceJsonConverter` pipeline.
- `DeserializeModuleContext(string)` — Runs a module context JSON string through the production `ModuleContextJsonConverter` pipeline.
- `ExtractModule<TModule>(IModuleWorker)` — Downcasts the worker's `IModule` reference to the expected concrete type.
- `ExecuteAsync(IModuleWorker, CancellationToken)` — Executes a module worker within the scope's runtime.
- `ExecuteAsync(ModuleContext, CancellationToken)` — Executes a full module context (with environment setup, configuration, and requires).

### CyborgTestBase

**File:** `Cyborg.Core.TestAdapter/CyborgTestBase.cs`

The public base class for all module unit tests. It is a façade that exposes a collection of higher-order function (HOF) methods as the primary test API. Each HOF method:

1. Resolves the module JSON source (explicit parameter or `GetDefaultModuleJsonAsync()` fallback).
2. Creates an empty `IServiceCollection` via `TestServiceConfiguration.CreateDefaultServices()`.
3. Calls `ConfigureServices(services, jabServiceDiscovery)`, which — via the override chain — registers the full production Jab DI graph for the domain under test.
4. Applies per-test-case service overrides (optional lambda).
5. Creates a `TestModuleRuntimeScope`.
6. Executes the test-specific pipeline (deserialization, validation, execution, etc.).
7. Invokes the caller-provided assertion lambda with the results.
8. Disposes the scope.

`CyborgTestBase.ConfigureServices` registers `ICyborgCoreServices` and a default silent `ILoggerFactory`. Domain-specific subclasses extend this by calling `base.ConfigureServices` and then registering their own Jab modules.

### Per-Domain Test Base Classes

Each test project provides a thin subclass of `CyborgTestBase` that registers the Jab modules relevant to that domain:

| Class | Project | Registers |
|-------|---------|----------|
| `ModuleTestBase` | `Cyborg.Modules.Tests` | `ICyborgModuleServices` |
| `BorgModuleTestBase` | `Cyborg.Modules.Borg.Tests` | `ICyborgModuleServices` + `ICyborgBorgServices` |

## Higher-Order Function API

All HOF methods follow a consistent pattern: they accept an optional `moduleJson` string, a required assertion callback (lambda), and optional configuration overrides. The assertion lambda receives only the data relevant to the specific test scenario, keeping test code focused and free of boilerplate.

Each HOF method provides two overloads:
- **Async overload** — accepts a `Func<..., Task>` assertion; this is the primary implementation.
- **Sync overload** — accepts an `Action<...>` assertion and delegates to the async overload; a convenience wrapper for simple test cases that don't require async assertions.

### Deserialization Testing

```csharp
protected Task TestDeserializationAsync<TModule>(
    string? moduleJson,
    Func<TModule, Task> assertion,          // async overload
    Action<IServiceCollection>? configureServices = null)

protected Task TestDeserializationAsync<TModule>(
    string? moduleJson,
    Action<TModule> assertion,              // sync overload
    Action<IServiceCollection>? configureServices = null)
```

Deserializes the module JSON and passes the typed module record to the assertion. Use this to verify polymorphic dispatch, property mapping, and JSON structure.

```csharp
protected Task TestContextDeserializationAsync(
    string? moduleContextJson,
    Func<ModuleContext, Task> assertion,    // async overload
    Action<IServiceCollection>? configureServices = null)

protected Task TestContextDeserializationAsync(
    string? moduleContextJson,
    Action<ModuleContext> assertion,        // sync overload
    Action<IServiceCollection>? configureServices = null)
```

Deserializes a full module context JSON and passes the `ModuleContext` to the assertion. Use this to verify environment, configuration, and requirements deserialization.

### Validation Testing

```csharp
protected Task TestValidationAsync<TModule>(
    string? moduleJson,
    Func<ValidationResult<TModule>, Task> assertion,  // async overload
    Action<IServiceCollection>? configureServices = null)

protected Task TestValidationAsync<TModule>(
    string? moduleJson,
    Action<ValidationResult<TModule>> assertion,      // sync overload
    Action<IServiceCollection>? configureServices = null)
```

Runs the full three-stage validation pipeline (defaults → overrides → constraints) and passes the `ValidationResult<TModule>` to the assertion. Use this to verify that validation attributes reject invalid inputs and that defaults are applied correctly.

```csharp
protected Task TestValidatedModuleAsync<TModule>(
    string? moduleJson,
    Func<TModule, TestModuleRuntimeScope, Task> assertion,
    Action<IServiceCollection>? configureServices = null)
```

Validates the module and asserts validity, then passes the validated module and runtime scope to the assertion for further inspection.

### Override Testing

```csharp
protected Task TestOverridesAsync<TModule>(
    string? moduleJson,
    Action<IRuntimeEnvironment> environmentSetup,
    Func<TModule, Task> assertion,          // async overload
    Action<IServiceCollection>? configureServices = null)

protected Task TestOverridesAsync<TModule>(
    string? moduleJson,
    Action<IRuntimeEnvironment> environmentSetup,
    Action<TModule> assertion,              // sync overload
    Action<IServiceCollection>? configureServices = null)
```

Configures environment variables via the `environmentSetup` callback, then runs validation (which includes override resolution) and passes the resolved module to the assertion. Use this to verify that `@module.property` overrides, variable interpolation, and default substitution work correctly.

### Module Execution Testing

```csharp
protected Task TestModuleAsync<TModule, TWorker>(
    string? moduleJson,
    Func<TModule, TWorker, IModuleExecutionResult, Task> assertion,  // async overload
    Action<IRuntimeEnvironment>? environmentSetup = null,
    Action<IServiceCollection>? configureServices = null)

protected Task TestModuleAsync<TModule, TWorker>(
    string? moduleJson,
    Action<TModule, TWorker, IModuleExecutionResult> assertion,      // sync overload
    Action<IRuntimeEnvironment>? environmentSetup = null,
    Action<IServiceCollection>? configureServices = null)
```

Deserializes, executes, and passes the module record, typed worker, and execution result to the assertion. This is the primary HOF for testing worker correctness, result publishing, and artifact exposure.

```csharp
protected Task TestExecutionAsync(
    string? moduleJson,
    Func<IModuleExecutionResult, Task> assertion,  // async overload
    Action<IRuntimeEnvironment>? environmentSetup = null,
    Action<IServiceCollection>? configureServices = null)

protected Task TestExecutionAsync(
    string? moduleJson,
    Action<IModuleExecutionResult> assertion,      // sync overload
    Action<IRuntimeEnvironment>? environmentSetup = null,
    Action<IServiceCollection>? configureServices = null)
```

Simplified variant that passes only the execution result to the assertion.

### Exception Testing

```csharp
protected Task TestExecutionThrowsAsync<TException>(
    string? moduleJson,
    Func<TException, Task>? assertion = null,  // async overload
    Action<IRuntimeEnvironment>? environmentSetup = null,
    Action<IServiceCollection>? configureServices = null)

protected Task TestExecutionThrowsAsync<TException>(
    string? moduleJson,
    Action<TException>? assertion,             // sync overload
    Action<IRuntimeEnvironment>? environmentSetup = null,
    Action<IServiceCollection>? configureServices = null)
```

Asserts that module execution throws an exception of the expected type. The optional assertion callback can inspect the thrown exception.

### Full Module Context Testing

```csharp
protected Task TestModuleContextAsync(
    string? moduleContextJson,
    Func<IModuleExecutionResult, TestModuleRuntimeScope, Task> assertion,
    Action<IRuntimeEnvironment>? environmentSetup = null,
    Action<IServiceCollection>? configureServices = null)
```

Deserializes and executes a full module context (with environment scoping, configuration modules, and requires). Passes the result and runtime scope to the assertion, enabling inspection of both the execution outcome and the environment state after execution.

## Service Configuration Model

### Per-Test-Class Configuration

Override `ConfigureServices(IServiceCollection, IJabServiceDiscovery)` in a derived test class to apply registrations that affect every test in the class. Always call `base.ConfigureServices` to preserve the registration chain:

```csharp
public sealed class MyModuleTests : ModuleTestBase
{
    protected override void ConfigureServices(IServiceCollection services, IJabServiceDiscovery jabServiceDiscovery)
    {
        base.ConfigureServices(services, jabServiceDiscovery);
        // Replace the process dispatcher with a mock for all tests in this class
        services.AddSingleton<IChildProcessDispatcher, MockChildProcessDispatcher>();
    }
}
```

### Per-Test-Case Configuration

Pass a `configureServices` lambda to any HOF method to apply registrations scoped to a single test invocation:

```csharp
[TestMethod]
public async Task MyTest()
{
    await TestModuleAsync<MyModule, MyModuleWorker>(
        moduleJson: "...",
        assertion: (module, worker, result) => { ... },
        configureServices: services =>
        {
            services.AddSingleton<IChildProcessDispatcher>(new FakeDispatcher(exitCode: 42));
        });
}
```

Per-test-case registrations are applied after per-test-class registrations, so they can override class-level defaults.

## Module JSON Source Resolution

Each HOF method resolves the module JSON source using a two-tier fallback:

1. **Explicit parameter** — If `moduleJson` is provided to the HOF method, it is used directly. This is the primary mechanism for test cases that need different JSON inputs.
2. **Default fallback** — If `moduleJson` is `null`, the framework calls `GetDefaultModuleJsonAsync()`, which derived classes can override to load JSON from a file, embedded resource, or other async source.

```csharp
public sealed class SubprocessModuleTests : ModuleTestBase
{
    protected override async ValueTask<string?> GetDefaultModuleJsonAsync()
    {
        // Load from a test fixture file
        return await File.ReadAllTextAsync("Fixtures/subprocess-default.json");
    }

    [TestMethod]
    public async Task DefaultJson_DeserializesCorrectly()
    {
        // Uses the default JSON from GetDefaultModuleJsonAsync()
        await TestDeserializationAsync<SubprocessModule>(
            moduleJson: null,
            module => Assert.AreEqual("/usr/bin/borg", module.Command.Executable));
    }

    [TestMethod]
    public async Task SpecificJson_OverridesDefault()
    {
        // Uses the explicitly provided JSON instead
        await TestDeserializationAsync<SubprocessModule>(
            moduleJson: """{ "cyborg.modules.subprocess.v1": { "command": { ... } } }""",
            module => Assert.AreEqual("/usr/bin/echo", module.Command.Executable));
    }
}
```

## Isolation Model

Each HOF method invocation creates a fully independent test scope:

- A fresh `IServiceCollection` is built from defaults.
- Per-test-class and per-test-case overrides are applied.
- A new `ServiceProvider` is constructed (not shared across tests).
- A new `RootModuleRuntime` and `GlobalRuntimeEnvironment` are created.
- The scope is disposed at the end of the test, releasing all resources.

This ensures complete isolation between tests, even when running in parallel (as enabled by `[Parallelize(Scope = ExecutionScope.MethodLevel)]` in `MSTestSettings.cs`).

## Design Decisions

### Reflection-Based Jab Discovery

The production codebase uses [Jab](https://github.com/pakrym/jab) for compile-time dependency injection, which generates code for each `[ServiceProvider]` or `[ServiceProviderModule]` interface. Jab generates its attribute types as `internal` within each referencing project — so `Cyborg.Core`'s `SingletonAttribute<T>` and `Cyborg.Modules`'s `SingletonAttribute<T>` are distinct CLR types even though they share the same name and namespace.

The `JabServiceDiscovery` class works around this by:
1. **Referencing Jab directly** in `Cyborg.Core.TestAdapter` to obtain stable, compiler-checked attribute names via `typeof(SingletonAttribute<>).Name` etc.
2. **Matching by name and namespace** using `CustomAttributeData`, not by type identity, so scanned attributes from any assembly are correctly identified regardless of which assembly's internal copy they originate from.

This approach was chosen over the alternative of manually duplicating registrations, which would be brittle and error-prone — adding a new service to a Jab module would silently break tests.

### Facade Pattern Over Monolith

`ModuleTestBase` is intentionally a thin façade that delegates to `TestServiceConfiguration`, `JabRegistrationDiscovery`, and `TestModuleRuntimeScope`. Each component has a single responsibility:

| Component | Responsibility |
|-----------|----------------|
| `JabServiceDiscovery` | Jab → MEDI attribute translation |
| `TestServiceConfiguration` | Empty service collection factory |
| `TestModuleRuntimeScope` | Per-test runtime lifecycle management |
| `CyborgTestBase` | HOF API, JSON resolution, core service registration |
| `ModuleTestBase` / `BorgModuleTestBase` | Domain-specific Jab module registration |

This separation ensures maintainability: changes to the DI translation logic do not affect the test API surface, and new HOF methods can be added to `ModuleTestBase` without touching the infrastructure components.

### InternalsVisibleTo

`[assembly: InternalsVisibleTo("...")]` entries are declared in `Cyborg.Core/_friends.cs` for each test project (`Cyborg.Core.Tests`, `Cyborg.Modules.Tests`, `Cyborg.Modules.Borg.Tests`) to grant access to `internal` runtime APIs (e.g., `IModuleWorker.ExecuteAsync`, `ScopedRuntime`, `ModuleExecutionResult`). This follows the existing pattern already used for `Cyborg.Core.Tests`.