using AElf;
using AElf.CSharp.Core;
using AElf.CSharp.Core.Extension;
using AElf.Types;

namespace Deployer;

public static class Extensions
{
    public static byte[] DecodeHex(this string hex)
    {
        return Enumerable.Range(0, hex.Length)
            .Where(x => x % 2 == 0)
            .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
            .ToArray();
    }

    public static Bloom GetBloom(this LogEvent logEvent)
    {
        var bloom = new Bloom();
        bloom.AddValue(logEvent.Address);
        bloom.AddValue(logEvent.Name.GetBytes());
        foreach (var t in logEvent.Indexed) bloom.AddValue(t.ToByteArray());

        return bloom;
    }


    public static InterestedEvent GetInterestedEvent<T>(this Address address) where T : IEvent<T>, new()
    {
        var logEvent = new T().ToLogEvent(address);
        return new InterestedEvent
        {
            LogEvent = logEvent,
            Bloom = logEvent.GetBloom()
        };
    }
}