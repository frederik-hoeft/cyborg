using Cyborg.Core.Configuration.Model;
using Cyborg.Core.Runtime.Engine;

namespace Cyborg.Core.Runtime;

public interface IModuleResultBuilder
{
    IModuleExecutionResult<TModule> Canceled<TModule>(TModule module) where TModule : ModuleBase, IModule<TModule>;

    IModuleExecutionResult<TModule> Canceled<TModule, TResult>(TModule module, TResult result) where TModule : ModuleBase, IModule<TModule> where TResult : class, IDecomposable;

    IModuleExecutionResult<TModule> Failed<TModule>(TModule module) where TModule : ModuleBase, IModule<TModule>;

    IModuleExecutionResult<TModule> Failed<TModule, TResult>(TModule module, TResult result) where TModule : ModuleBase, IModule<TModule> where TResult : class, IDecomposable;

    IModuleExecutionResult<TModule> Skipped<TModule>(TModule module) where TModule : ModuleBase, IModule<TModule>;

    IModuleExecutionResult<TModule> Skipped<TModule, TResult>(TModule module, TResult result) where TModule : ModuleBase, IModule<TModule> where TResult : class, IDecomposable;

    IModuleExecutionResult<TModule> Success<TModule>(TModule module) where TModule : ModuleBase, IModule<TModule>;

    IModuleExecutionResult<TModule> Success<TModule, TResult>(TModule module, TResult result) where TModule : ModuleBase, IModule<TModule> where TResult : class, IDecomposable;

    IModuleExecutionResult<TModule> WithStatus<TModule>(TModule module, ModuleExitStatus status) where TModule : ModuleBase, IModule<TModule>;

    IModuleExecutionResult<TModule> WithStatus<TModule, TResult>(TModule module, ModuleExitStatus status, TResult result) where TModule : ModuleBase, IModule<TModule> where TResult : class, IDecomposable;
}
