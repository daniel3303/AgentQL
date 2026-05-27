using System.Text;
using AwesomeAssertions;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Xunit;

namespace Equibles.AgentQL.UnitTests.Query;

/// <summary>
/// Locks in the per-type byte estimates that drive the MaxBytes cap in
/// QueryExecutor. Each .NET primitive maps to a fixed width; strings count
/// UTF-8 bytes (not UTF-16 chars) so the estimate matches the on-the-wire
/// payload size; unknown types fall through to a conservative stringified
/// estimate that biases toward refusing borderline-sized results.
/// </summary>
public class ResultByteEstimatorTests
{
    [Fact]
    public void EstimateBytes_Null_ReturnsZero()
    {
        ResultByteEstimator.EstimateBytes(null).Should().Be(0);
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("a", 1)]
    [InlineData("hello", 5)]
    public void EstimateBytes_AsciiString_ReturnsCharCount(string value, long expected)
    {
        ResultByteEstimator.EstimateBytes(value).Should().Be(expected);
    }

    [Fact]
    public void EstimateBytes_MultiByteUtf8String_CountsBytesNotChars()
    {
        const string value = "héllo";
        var expected = Encoding.UTF8.GetByteCount(value);
        expected.Should().Be(6);
        ResultByteEstimator.EstimateBytes(value).Should().Be(expected);
    }

    [Fact]
    public void EstimateBytes_ByteArray_ReturnsLength()
    {
        var bytes = new byte[] { 1, 2, 3, 4, 5 };
        ResultByteEstimator.EstimateBytes(bytes).Should().Be(5);
    }

    [Fact]
    public void EstimateBytes_EmptyByteArray_ReturnsZero()
    {
        ResultByteEstimator.EstimateBytes(Array.Empty<byte>()).Should().Be(0);
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 1)]
    public void EstimateBytes_Bool_ReturnsOne(bool value, long expected)
    {
        ResultByteEstimator.EstimateBytes(value).Should().Be(expected);
    }

    [Fact]
    public void EstimateBytes_Char_ReturnsTwo()
    {
        ResultByteEstimator.EstimateBytes('A').Should().Be(2);
    }

    [Theory]
    [InlineData((short)42, 2)]
    [InlineData((ushort)42, 2)]
    public void EstimateBytes_SixteenBitIntegers_ReturnTwo(object value, long expected)
    {
        ResultByteEstimator.EstimateBytes(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(42, 4)]
    [InlineData(42u, 4)]
    public void EstimateBytes_ThirtyTwoBitIntegers_ReturnFour(object value, long expected)
    {
        ResultByteEstimator.EstimateBytes(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(42L, 8)]
    [InlineData(42UL, 8)]
    public void EstimateBytes_SixtyFourBitIntegers_ReturnEight(object value, long expected)
    {
        ResultByteEstimator.EstimateBytes(value).Should().Be(expected);
    }

    [Fact]
    public void EstimateBytes_Float_ReturnsFour()
    {
        ResultByteEstimator.EstimateBytes(3.14f).Should().Be(4);
    }

    [Fact]
    public void EstimateBytes_Double_ReturnsEight()
    {
        ResultByteEstimator.EstimateBytes(3.14).Should().Be(8);
    }

    [Fact]
    public void EstimateBytes_Decimal_ReturnsSixteen()
    {
        ResultByteEstimator.EstimateBytes(3.14m).Should().Be(16);
    }

    [Fact]
    public void EstimateBytes_Guid_ReturnsSixteen()
    {
        ResultByteEstimator.EstimateBytes(Guid.NewGuid()).Should().Be(16);
    }

    [Fact]
    public void EstimateBytes_DateTime_ReturnsEight()
    {
        ResultByteEstimator.EstimateBytes(DateTime.UtcNow).Should().Be(8);
    }

    [Fact]
    public void EstimateBytes_DateTimeOffset_ReturnsTwelve()
    {
        ResultByteEstimator.EstimateBytes(DateTimeOffset.UtcNow).Should().Be(12);
    }

    [Fact]
    public void EstimateBytes_TimeSpan_ReturnsEight()
    {
        ResultByteEstimator.EstimateBytes(TimeSpan.FromMinutes(1)).Should().Be(8);
    }

    [Fact]
    public void EstimateBytes_UnknownType_FallsBackToToStringUtf8ByteCount()
    {
        // Uri is not in the switch list; the fallback path stringifies it and
        // measures the UTF-8 byte length — a conservative upper bound that
        // biases toward refusing borderline-sized results.
        var uri = new Uri("https://example.com/path?q=hello");
        var expected = Encoding.UTF8.GetByteCount(uri.ToString());
        ResultByteEstimator.EstimateBytes(uri).Should().Be(expected);
    }

    [Fact]
    public void EstimateBytes_UnknownTypeWithNullToString_ReturnsZero()
    {
        // ToString() returning null is rare but possible for custom types.
        // The fallback path uses ?? string.Empty so the result is zero, not
        // a NullReferenceException.
        var value = new TypeWithNullToString();
        ResultByteEstimator.EstimateBytes(value).Should().Be(0);
    }

    private sealed class TypeWithNullToString
    {
        public override string ToString() => null;
    }
}
