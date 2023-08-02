using AElf.Contracts.MultiToken;
using Google.Protobuf.WellKnownTypes;
using Rule;

namespace MultiToken.Rules.DailyLimit;


public partial class Rule
{
    private static BoolValue Ok() => new BoolValue
    {
        Value = true
    };

    private static BoolValue NotOk() => new BoolValue
    {
        Value = false
    };

    private static bool Unpack(CallContext callContext, out TransferInput transferInput)
    {
        transferInput = null;

        if (callContext.MethodName != nameof(TokenContractContainer.TokenContractReferenceState.Transfer))
        {
            return false;
        }

        transferInput = TransferInput.Parser.ParseFrom(callContext.Args);
        return true;
    }
}