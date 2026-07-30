# Module Testing Architecture

This document describes the design and architecture of the reusable unit testing adapter in `Cyborg.Modules.Tests`. The adapter provides a base test class for MSTest that abstracts away module loading, DI container construction, and runtime initialization, enabling per-module unit test classes with minimal boilerplate.

**Table of Contents**

<!-- @import "[TOC]" {cmd="toc" depthFrom=2 depthTo=6 orderedList=false} -->

<!-- code_chunk_output -->

- [Overview](#overview)
- [Component Architecture](#component-architecture)
  - [Component Diagram](#component-diagram)
  - [JabRegistrationDiscovery](#jabregistrationdiscovery)
  - [TestServiceConfiguration](#testserviceconfiguration)
  - [TestModuleRuntimeScope](#testmoduleruntimescope)
  - [ModuleTestBase](#moduletestbase)
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

The adapter is structured as a layered set of components, each with a single responsibility, composed behind the `ModuleTestBase` façade class.

## Component Architecture

### Component Diagram

```
┌──────────────────────────────────────────────────────────────────────────┐
│  Per-Module Test Class (extends ModuleTestBase)                         │
│  ┌────────────────────────────────────────────────────────────────────┐  │
│  │  TestMethod: calls HOF methods with module JSON + assertion       │  │
│  └────────────────────────────────┬───────────────────────────────────┘  │
└───────────────────────────────────┼──────────────────────────────────────┘
                                    │
                    ┌───────────────▼──────────────┐
                    │       ModuleTestBase          │
                    │  (HOF façade, public API)     │
                    │                               │
                    │  • TestDeserializationAsync   │
                    │  • TestValidationAsync        │
                    │  • TestOverridesAsync         │
                    │  • TestModuleAsync            │
                    │  • TestExecutionAsync         │
                    │  • TestExecutionThrowsAsync   │
                    │  • TestModuleContextAsync     │
                    └──────┬────────────┬───────────┘
                           │            │
        ┌──────────────────▼──┐  ┌──────▼──────────────────────┐
        │ TestService-         │  │ TestModuleRuntimeScope      │
        │ Configuration        │  │                             │
        │                      │  │  • Runtime + Environment    │
        │  CreateDefault-      │  │  • DeserializeModule()      │
        │    Services()        │  │  • ExecuteAsync()           │
        └──────────┬───────────┘  │  • IAsyncDisposable         │
                   │              └──────────┬──────────────────┘
        ┌──────────▼───────────┐             │
        │ JabRegistration-     │             │ uses
        │ Discovery            │             │
        │                      │   ┌─────────▼────────────────┐
        │  Reflects over Jab   │   │  Production runtime:     │
        │  [Singleton<>],      │   │  RootModuleRuntime,      │
        │  [Scoped<>],         │   │  GlobalRuntimeEnvironment│
        │  [Transient<>] and   │   │  IJsonLoaderContext,     │
        │  [Import<>] attrs    │   │  IModuleLoaderRegistry   │
        │  to build MEDI       │   └──────────────────────────┘
        │  registrations       │
        └──────────────────────┘
```

### JabRegistrationDiscovery

**File:** `Infrastructure/JabRegistrationDiscovery.cs`

Translates Jab's compile-time DI declarations into MEDI runtime registrations via reflection. Jab generates its attribute types as `internal` within each project, so this class uses `CustomAttributeData` and matches attributes by their unbound generic name in the `Jab` namespace (e.g., `SingletonAttribute\`1`, `ImportAttribute\`1`) rather than direct type comparison.

Key behaviors:
- Recursively processes `[Import<TModule>]` references (depth-first) to capture the full service graph across module boundaries (`ICyborgCoreServices` → `IDynamicValueProviderServices`, `IConfigurationTrustServices`, etc.).
- Supports all three Jab service lifetimes: `[Singleton<T>]`, `[Scoped<T>]`, and `[Transient<T>]` (each in one- and two-type-argument forms).
- Handles factory-based registrations by invoking the static factory method on the declaring interface with parameters resolved from the service provider.
- Handles constructor-based registrations via `ActivatorUtilities.CreateInstance`.

### TestServiceConfiguration

**File:** `Infrastructure/TestServiceConfiguration.cs`

Provides a single static factory method `CreateDefaultServices()` that builds an `IServiceCollection` pre-populated with all production Jab modules (`ICyborgCoreServices`, `ICyborgModuleServices`, `ICyborgBorgServices`) plus a default silent `ILoggerFactory`. The returned collection is mutable, enabling callers to override or extend registrations before the service provider is built.

### TestModuleRuntimeScope

**File:** `Infrastructure/TestModuleRuntimeScope.cs`

Encapsulates the per-test lifecycle: builds a `ServiceProvider` from the configured `IServiceCollection`, constructs a `RootModuleRuntime` with a fresh `GlobalRuntimeEnvironment`, and provides methods for module deserialization and execution. Implements `IAsyncDisposable` for deterministic cleanup.

Key methods:
- `Create(IServiceCollection)` — Static factory that builds the scope from a configured service collection.
- `DeserializeModule(string)` — Runs a module JSON string through the production `ModuleReferenceJsonConverter` pipeline.
- `DeserializeModuleContext(string)` — Runs a module context JSON string through the production `ModuleContextJsonConverter` pipeline.
- `ExtractModule<TModule>(IModuleWorker)` — Downcasts the worker's `IModule` reference to the expected concrete type.
- `ExecuteAsync(IModuleWorker, CancellationToken)` — Executes a module worker within the scope's runtime.
- `ExecuteAsync(ModuleContext, CancellationToken)` — Executes a full module context (with environment setup, configuration, and requires).

### ModuleTestBase

**File:** `Infrastructure/ModuleTestBase.cs`

The public base class for per-module unit tests. It is a façade that exposes a collection of higher-order function (HOF) methods as the primary test API. Each HOF method:

1. Resolves the module JSON source (explicit parameter or `GetDefaultModuleJsonAsync()` fallback).
2. Builds a fresh `IServiceCollection` via `TestServiceConfiguration`, applies per-test-class overrides (`ConfigureServices`), then per-test-case overrides (optional lambda).
3. Creates a `TestModuleRuntimeScope`.
4. Executes the test-specific pipeline (deserialization, validation, execution, etc.).
5. Invokes the caller-provided assertion lambda with the results.
6. Disposes the scope.

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

Override `ConfigureServices(IServiceCollection)` in a derived test class to apply registrations that affect every test in the class:

```csharp
public sealed class MyModuleTests : ModuleTestBase
{
    protected override void ConfigureServices(IServiceCollection services)
    {
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

The production codebase uses [Jab](https://github.com/pakrym/jab) for compile-time dependency injection, which generates code for each `[ServiceProvider]` or `[ServiceProviderModule]` interface. Since Jab generates its attribute types as `internal` within each project, the test project cannot directly reference them. The `JabRegistrationDiscovery` class uses `CustomAttributeData` and string-based attribute type matching to scan the production interfaces and translate their declarations into MEDI registrations.

This approach was chosen over alternatives:
- **Manually duplicating registrations** — Brittle and error-prone; adding a new service to a Jab module would silently break tests.
- **Adding Jab to the test project** — Would introduce unnecessary source generation and constraint the test project's DI to compile-time patterns.
- **Using the production `DefaultServiceProvider`** — The Jab-generated service provider is `internal` and `sealed`, making it inaccessible from the test project without exposing production internals.

### Facade Pattern Over Monolith

`ModuleTestBase` is intentionally a thin façade that delegates to `TestServiceConfiguration`, `JabRegistrationDiscovery`, and `TestModuleRuntimeScope`. Each component has a single responsibility:

| Component | Responsibility |
|-----------|----------------|
| `JabRegistrationDiscovery` | Jab → MEDI attribute translation |
| `TestServiceConfiguration` | Default service collection assembly |
| `TestModuleRuntimeScope` | Per-test runtime lifecycle management |
| `ModuleTestBase` | HOF API and JSON resolution |

This separation ensures maintainability: changes to the DI translation logic do not affect the test API surface, and new HOF methods can be added to `ModuleTestBase` without touching the infrastructure components.

### InternalsVisibleTo

A single `[assembly: InternalsVisibleTo("Cyborg.Modules.Tests")]` was added to `Cyborg.Core/_friends.cs` to grant the test project access to `internal` runtime APIs (e.g., `IModuleWorker.ExecuteAsync`, `ScopedRuntime`, `ModuleExecutionResult`). This is a minimal, non-breaking change that follows the existing pattern used for `Cyborg.Core.Tests`.