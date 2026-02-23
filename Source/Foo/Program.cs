using Cyborg.Core.Modules;
using Foo;
using System.IO.Pipes;

using DefaultServiceProvider sp = new();
IConfigurationLoader configurationLoader = sp.GetService<IConfigurationLoader>();
IModule module = await configurationLoader.LoadMainModuleAsync("config.json", CancellationToken.None);
await module.ExecuteAsync(null, CancellationToken.None);