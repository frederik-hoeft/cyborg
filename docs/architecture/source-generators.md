# Source Generator Architecture

<!-- @import "[TOC]" {cmd="toc" depthFrom=2 depthTo=6 orderedList=false} -->

## Generator Contract Registrations

Source generators in `Cyborg.Core.Aot` need to reference runtime types from `Cyborg.Core` (e.g., `IModuleRuntime`, `ValidationResult<T>`) when emitting code. Since generators target `netstandard2.0` and cannot directly reference `net10.0` assemblies, a contract registration system bridges this gap.

**Problem**: Generators cannot hardcode fully-qualified type names because:
- Types may be renamed, moved, or refactored
- Generic arity and namespace changes would break generated code
- No compile-time verification that referenced types exist

**Solution**: Runtime types register themselves with contract enums; generators discover registrations at compile time.

```
Cyborg.Core.Aot                          Cyborg.Core
┌────────────────────────────┐           ┌─────────────────────────────────────────────┐
│ enum ModuleValidation-     │           │ [GeneratorContractRegistration<             │
│   GeneratorContract {      │◄──────────│   ModuleValidationGeneratorContract>(       │
│   IModuleRuntime,          │           │   ModuleValidationGeneratorContract         │
│   IModuleT,                │           │     .IModuleRuntime)]                       │
│   ValidationResultT,       │           │ public interface IModuleRuntime { ... }     │
│   ValidationError,         │           └─────────────────────────────────────────────┘
│   IDefaultValueT           │
│ }                          │
│                            │
│ ContractExplorer           │──────────► Scans all assemblies for
│   .DiscoverContracts<T>()  │            [GeneratorContractRegistration<T>]
│                            │            attributes and builds
│                            │            Dictionary<TContract, INamedTypeSymbol>
└────────────────────────────┘
```

**Bootstrap**: `ContractRegistrationBootstrapGenerator` emits the contract enum definitions and attribute into the compilation via `RegisterPostInitializationOutput`, making them available to consuming assemblies without direct project references.

**Why this pattern?**
- Compile-time discovery: generators fail fast if a contract registration is missing
- Refactor-safe: renaming types in `Cyborg.Core` automatically updates generated code
- Single source of truth: each type self-declares its contract role

## ModuleLoaderFactoryGenerator

Generates `CreateWorker()` implementations that resolve constructor dependencies:

```csharp
// Input (developer writes):
[GeneratedModuleLoaderFactory]
public sealed partial class FooModuleLoader(IServiceProvider sp)
    : ModuleLoader<FooModuleWorker, FooModule>(sp);

// Output (generated):
partial class FooModuleLoader
{
    protected override FooModuleWorker CreateWorker(FooModule module, IServiceProvider serviceProvider)
    {
        return new FooModuleWorker(
            module,
            serviceProvider.GetRequiredService<IOtherDependency>(),
            // ... additional constructor parameters resolved from DI
        );
    }
}
```

**Why generate this?**
- AOT requires avoiding `Activator.CreateInstance` and reflection-based DI
- Constructor parameters are analyzed at compile time
- Module type is passed directly; other parameters resolve from `IServiceProvider`
- Less boilerplate for developers creating new modules (no manual calls to `GetRequiredService`)

## ModuleValidationGenerator

Generates `IModule<TModule>` implementations that integrate with the [Module Execution Lifecycle](module-system.md#module-execution-lifecycle). The generator analyzes module record definitions and emits the three validation pipeline methods.

**Developer Input:**

```csharp
[GeneratedModuleValidation]
public sealed partial record WakeOnLanModule(
    [property: Required] string TargetHost,
    [property: Required][property: ExactLength(17)] string MacAddress,
    [property: Range<int>(Min = 1, Max = 65535)] int LivenessProbePort,
    [property: DefaultTimeSpan("00:05:00")] TimeSpan MaxWaitTime,
    ModuleArtifacts Artifacts   // Inherited from ModuleBase, validated recursively
) : ModuleBase, IModule;
```

**Generated Contract:**

The generator emits a partial record implementing `IModule<WakeOnLanModule>` with:
- `ApplyDefaultsAsync()` — Fills `default!` values from `[DefaultValue<T>]` / `[DefaultTimeSpan]` annotations
- `ResolveOverridesAsync()` — Substitutes `${VAR}` references and `@module.property` overrides from runtime environment
- `ValidateAsync()` — Checks `[Required]`, `[Range<T>]`, `[ExactLength]`, enum validity; returns `ValidationResult<T>`

**Design Characteristics:**
- **Recursive** — Nested records (e.g., `ModuleArtifacts.Environment`) are processed depth-first
- **Immutable** — Returns new instances via `with` expressions
- **AOT-safe** — No reflection; all validation logic is compile-time generated
- **Fail-fast** — Collects all errors before returning `Invalid(errors)`
