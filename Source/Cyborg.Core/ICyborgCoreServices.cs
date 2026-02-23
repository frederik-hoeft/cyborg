using System.Text.Json.Serialization;
using Jab;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Configuration;
using Cyborg.Core.Services;
using Cyborg.Core.Modules.Runtime;

namespace Cyborg.Core;

[ServiceProviderModule]
[Singleton<INamedServiceProvider, NamedServiceProvider>]
[Singleton<IModuleLoaderContext, DefaultModuleLoaderContext>]
[Singleton<JsonConverter, ModuleReferenceJsonConverter>]
[Singleton<IModuleLoaderRegistry, DefaultModuleLoaderRegistry>]
[Singleton<IModuleConfigurationLoader, DefaultModuleConfigurationLoader>]
[Singleton<IModuleRuntime, DefaultModuleRuntime>]
[Singleton<DefaultEnvironment>]
public interface ICyborgCoreServices;