using Jab;
using SmartmonExporter.Configuration;
using SmartmonExporter.Configuration.Named;
using SmartmonExporter.Domain;

namespace SmartmonExporter;

[ServiceProvider]
[Singleton<INamedServiceProvider, NamedServiceProvider>]
[Import<IConfigurationModule>]
[Import<ISmartMetricsModule>]
internal sealed partial class DefaultServiceProvider;