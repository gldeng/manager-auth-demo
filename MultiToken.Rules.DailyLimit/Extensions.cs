using System.Collections.Generic;
using System.Linq;

namespace MultiToken.Rules.DailyLimit;

public static class Extensions
{
    public static ulong SumUlong(this IEnumerable<ulong> values)
    {
        return values.Aggregate(0ul, (current, value) => current + value);
    }
}