using System.Diagnostics.CodeAnalysis;
using System.Linq;
using AElf.CSharp.Core.Extension;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace MultiToken.Rules.DailyLimit;

[SuppressMessage("ReSharper", "UnusedType.Global")]
public partial class Rule : DailyLimitRuleContainer.DailyLimitRuleBase
{

    public override BoolValue Check(CallContext callContext)
    {
        if (!Unpack(callContext, out var transferInput))
        {
            return NotOk();
        }

        // TODO: Change to a fixed cutoff
        var _24HoursAgo = Context.CurrentBlockTime.AddMinutes(-1440);

        var tracker = State.ManagerQuotaTrackers[Context.Sender][callContext.Manager][transferInput.Symbol];

        var within24Hours = tracker.TransferRecords.Where(rec => rec.Timestamp.CompareTo(_24HoursAgo) > 0).ToList();
        var usedQuota = within24Hours.Select(rec => rec.Amount).SumUlong();
        var ok = (usedQuota + (ulong)transferInput.Amount) < tracker.Limit;

        // Update tracker
        tracker.TransferRecords.Add(new TransferRecord
        {
            Amount = (ulong) transferInput.Amount,
            Timestamp = Context.CurrentBlockTime,
        });
        State.ManagerQuotaTrackers[Context.Sender][callContext.Manager][transferInput.Symbol] = tracker;
        
        return ok ? Ok() : NotOk();
    }

    public override Empty Configure(BytesValue data)
    {
        var input = ConfigureInput.Parser.ParseFrom(data.Value.ToByteArray());
        var tracker = State.ManagerQuotaTrackers[Context.Sender][input.Manager][input.Symbol];
        tracker.Limit = input.Limit;
        State.ManagerQuotaTrackers[Context.Sender][input.Manager][input.Symbol] = tracker;
        return new Empty();
    }
}

