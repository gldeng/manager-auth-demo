#pragma warning disable CS0649
using AElf.Sdk.CSharp.State;
using AElf.Types;

namespace MultiToken.Rules.DailyLimit;

public class RuleState : ContractState
{
    internal MappedState<Address, Address, string, ManagerQuotaTracker> ManagerQuotaTrackers;
}