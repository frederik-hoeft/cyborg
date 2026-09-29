using Cyborg.Core.Configuration.Serialization.Dynamics.Providers;

namespace Cyborg.Core.Runtime.Services.Transactions;

/// <summary>Parses <c>cyborg.types.core.transactions.on_error.v1</c> configuration values.</summary>
public sealed class DynamicTransactionOnErrorProvider() : DynamicEnumValueProvider<TransactionOnError>("cyborg.types.core.transactions.on_error.v1");
