using AElf.Sdk.CSharp.State;
using AElf.Standards.ACS1;
using AElf.Types;
using AElf.Contracts.A;

namespace Portkey.Contracts.CA;

public class CAContractState : ContractState
{
    public MappedState<Hash, ManagerInfoList> AccountManagers { get; set; }
    public SingletonState<string> AllRules {get; set;}
    public MappedState<string, Address> RuleAddresses {get; set;}

    // From -> Destination Contract -> Policies
    public MappedState<Hash, Address, string> Policies { get; set; }
}