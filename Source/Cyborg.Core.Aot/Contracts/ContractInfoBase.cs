using Microsoft.CodeAnalysis;
using System.Collections.Frozen;

namespace Cyborg.Core.Aot.Contracts;

internal abstract class ContractInfoBase<TContract>(Dictionary<TContract, INamedTypeSymbol> contractTypes) where TContract : unmanaged, Enum
{
    public FrozenDictionary<TContract, INamedTypeSymbol> ContractTypes { get; } = contractTypes.ToFrozenDictionary(kv => kv.Key, kv => kv.Value, EqualityComparer<TContract>.Default);

    protected static Dictionary<TContract, INamedTypeSymbol>? FetchContracts(ContractExplorer contractExplorer, SourceProductionContext context, FrozenSet<TContract> knownContracts)
    {
        Dictionary<TContract, INamedTypeSymbol> contracts = contractExplorer.GetContracts<TContract>(context);
        bool failed = false;
        foreach (KeyValuePair<TContract, INamedTypeSymbol> kvp in contracts)
        {
            if (!knownContracts.Contains(kvp.Key))
            {
                context.ReportDiagnostic(Diagnostic.Create(Diagnostics.UnknownContract,
                    kvp.Value.Locations.FirstOrDefault(),
                    kvp.Key.ToString(),
                    kvp.Value.ToDisplayString()));
                failed = true;
            }
        }
        foreach (TContract contract in knownContracts)
        {
            if (!contracts.ContainsKey(contract))
            {
                context.ReportDiagnostic(Diagnostic.Create(Diagnostics.MissingContract,
                    Location.None,
                    contract.ToString(),
                    typeof(TContract).Name));
                failed = true;
            }
        }
        if (failed)
        {
            return null;
        }
        return contracts;
    }
}