using Cyborg.Core.Configuration.Serialization.Dynamics.Providers;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Configuration.Serialization.Dynamics.ValueProviders;
using Jab;

namespace Cyborg.Core.Configuration.Serialization.Dynamics;

[ServiceProviderModule]
[Singleton<IDynamicGenericValueProviderFactory, CollectionDynamicValueProviderFactory>]
[Singleton<IDynamicValueProviderRegistry, DynamicValueProviderRegistry>]
[Singleton<IDynamicValueProvider, DynamicSByteProvider>]
[Singleton<IDynamicValueProvider, DynamicByteProvider>]
[Singleton<IDynamicValueProvider, DynamicInt16Provider>]
[Singleton<IDynamicValueProvider, DynamicUInt16Provider>]
[Singleton<IDynamicValueProvider, DynamicInt32Provider>]
[Singleton<IDynamicValueProvider, DynamicUInt32Provider>]
[Singleton<IDynamicValueProvider, DynamicInt64Provider>]
[Singleton<IDynamicValueProvider, DynamicUInt64Provider>]
[Singleton<IDynamicValueProvider, DynamicSingleProvider>]
[Singleton<IDynamicValueProvider, DynamicDoubleProvider>]
[Singleton<IDynamicValueProvider, DynamicDecimalProvider>]
[Singleton<IDynamicValueProvider, DynamicBooleanProvider>]
[Singleton<IDynamicValueProvider, DynamicStringProvider>]
public interface IDynamicValueProviderServices;