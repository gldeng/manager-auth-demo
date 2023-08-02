using AElf.Sdk.CSharp;
using AElf.Types;
using Ca;
using Google.Protobuf.WellKnownTypes;
using Rule;

namespace Portkey.Contracts.CA;

public class CAContract : CAContractContainer.CAContractBase
{
    public override Empty SetPolicy(SetPolicyInput input)
    {
        // TODO: check permission and policy valid
        State.Policies[input.CaHash][input.Destination] = input.Policy;
        return new Empty();
    }

    public override Empty SetRuleAddress(SetRuleAddressInput input)
    {
        var existing = State.RuleAddresses[input.RuleName];
        Assert(existing.Equals(new Address()), "rule already exists");
        State.RuleAddresses[input.RuleName] = input.Address;
        return new Empty();
    }

    public override Empty UpdateManagers(UpdateManagersInput input)
    {
        // Note: for demo purpose, authorization is not checked.
        State.AccountManagers[input.CaHash] = input.ManagerInfoList;
        return new Empty();
    }

    public override Empty Call(CallInput input)
    {
        var policy = State.Policies[input.CaHash][input.ContractAddress];
        var ruleAddress = State.RuleAddresses[policy];
        Assert(!ruleAddress.Equals(new Address()), "rule doesn't exist");
        Context.SendVirtualInline(input.CaHash, ruleAddress, nameof(RuleContainer.RuleReferenceState.Check), 
            new CallContext
            {
                Args = input.Args,
                CaHash = input.CaHash,
                ContractAddress = input.ContractAddress,
                Manager = Context.Sender,
                MethodName = input.MethodName,
            });
        return new Empty();
    }
}