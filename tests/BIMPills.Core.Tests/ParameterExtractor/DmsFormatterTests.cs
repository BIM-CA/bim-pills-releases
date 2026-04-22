using BIMPills.Commands.ParameterExtractor;
using Xunit;

namespace BIMPills.Core.Tests.ParameterExtractor
{
    public class DmsFormatterTests
    {
        private readonly DmsFormatter _sut = new();

        [Theory]
        [InlineData(40.4168, true, "40°25'00.48\"N")]
        [InlineData(-40.4168, true, "40°25'00.48\"S")]
        [InlineData(-3.7038, false, "3°42'13.68\"W")]
        [InlineData(3.7038, false, "3°42'13.68\"E")]
        [InlineData(0.0, true, "0°00'00.00\"N")]
        public void Format_ProducesExpectedString(double dec, bool isLat, string expected)
        {
            Assert.Equal(expected, _sut.Format(dec, isLat));
        }

        [Theory]
        [InlineData("40°25'00.48\"N", 40.4168)]
        [InlineData("40°25'00.48\"S", -40.4168)]
        [InlineData("3°42'13.68\"W", -3.7038)]
        [InlineData("3°42'13.68\"E", 3.7038)]
        public void TryParse_ValidDms_ReturnsDecimal(string dms, double expected)
        {
            Assert.True(_sut.TryParse(dms, out double actual));
            Assert.Equal(expected, actual, 4);
        }

        [Fact]
        public void TryParse_Invalid_ReturnsFalse()
        {
            Assert.False(_sut.TryParse("not a coord", out _));
            Assert.False(_sut.TryParse("", out _));
            Assert.False(_sut.TryParse(null!, out _));
        }

        [Theory]
        [InlineData(40.4168, true)]
        [InlineData(-40.4168, true)]
        [InlineData(-3.7038, false)]
        [InlineData(0.123456, true)]
        public void RoundTrip_DecimalToDmsToDecimal_PreservesValue(double original, bool isLat)
        {
            var dms = _sut.Format(original, isLat, secondsDecimals: 4);
            Assert.True(_sut.TryParse(dms, out double parsed));
            Assert.Equal(original, parsed, 4);
        }

        [Fact]
        public void Format_SecondsRoundingCarriesToMinutes()
        {
            // 1°59'59.999" con 2 decimales redondea a 2°00'00.00"N (no 1°59'60.00").
            double almost2 = 1 + 59.0 / 60.0 + 59.999 / 3600.0;
            var result = _sut.Format(almost2, true, secondsDecimals: 2);
            Assert.Equal("2°00'00.00\"N", result);
        }
    }
}
