using System.Text;

namespace Equibles.AgentQL.EntityFrameworkCore.Query;

// Estimates the byte cost of materialising one column value so the
// QueryExecutor read loop can stop early on oversized payloads. Strings
// count their UTF-8 byte length (what the value would weigh on the wire
// or in JSON); byte arrays count their raw length; primitives use their
// fixed widths. Unknown types fall back to a stringified estimate — a
// conservative upper bound that biases toward refusing borderline-sized
// results.
internal static class ResultByteEstimator
{
    public static long EstimateBytes(object value) =>
        value switch
        {
            null => 0,
            string s => Encoding.UTF8.GetByteCount(s),
            byte[] b => b.Length,
            bool => 1,
            char => 2,
            short => 2,
            ushort => 2,
            int => 4,
            uint => 4,
            long => 8,
            ulong => 8,
            float => 4,
            double => 8,
            decimal => 16,
            Guid => 16,
            DateTime => 8,
            DateTimeOffset => 12,
            TimeSpan => 8,
            _ => Encoding.UTF8.GetByteCount(value.ToString() ?? string.Empty),
        };
}
