using Cyborg.Core.Configuration.Serialization.Dynamics;
using Cyborg.Core.Modules.Debugging.Breakpoints;
using Cyborg.Core.Modules.Debugging.Configuration;
using Cyborg.Core.Modules.Descriptors;
using Cyborg.Core.Modules.Descriptors.Builders;
using Cyborg.Core.Modules.Descriptors.Model;
using Cyborg.Core.Modules.Descriptors.Writers;
using Cyborg.Core.Services.Default;
using Jab;

namespace Cyborg.Core.Modules.Debugging;

[ServiceProviderModule]
[Singleton<IDynamicValueProvider, DebugOptionsProvider>]
[Singleton<IModuleDescriptionSerializer, TextModuleDescriptionSerializer>]
[Singleton<IModuleDescriptionSerializer, JsonModuleDescriptionSerializer>]
[Singleton<IModuleDescriptionSerializerRegistry, DefaultModuleDescriptionSerializerRegistry>]
[Singleton<IDescriptionComponentFactory, DefaultDescriptionComponentFactory>]
[Transient<IObjectDescriptionBuilder, ObjectDescriptionBuilder>]
[Singleton<IObjectDescriptionBuilderFactory, DIObjectDescriptionBuilderFactory>]
[Singleton<IModuleSerializationService, DefaultModuleSerializationService>]
[Singleton<IBreakpointRegistry, BreakpointRegistry>]
[Singleton<IWorkflowDebugger, WorkflowDebugger>]
[Singleton<IServiceSelectionKey<IDebugFrontend>>(Instance = nameof(DebugFrontendSelectionKey))]
[Singleton<IDefault<IDebugFrontend>, Default<IDebugFrontend>>]
public interface IDebugServices
{
    static ServiceSelectionKey<IDebugFrontend> DebugFrontendSelectionKey => new("cyborg.core.debug:frontend");
}
