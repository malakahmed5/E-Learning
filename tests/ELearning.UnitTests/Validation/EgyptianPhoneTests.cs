using ELearning.Shared.Validation;

namespace ELearning.UnitTests.Validation
{
    public class EgyptianPhoneTests
    {
        [Theory]
        [InlineData("01012345678", "01012345678")]
        [InlineData("01112345678", "01112345678")]
        [InlineData("01212345678", "01212345678")]
        [InlineData("01512345678", "01512345678")]
        [InlineData("+201012345678", "01012345678")]
        [InlineData("00201012345678", "01012345678")]
        [InlineData("201012345678", "01012345678")]
        [InlineData("010 1234 5678", "01012345678")]
        [InlineData("010-1234-5678", "01012345678")]
        [InlineData("+20 10 1234 5678", "01012345678")]
        public void TryNormalize_accepts_valid_numbers_and_stores_one_form(string typed, string expected)
        {
            var isValid = EgyptianPhone.TryNormalize(typed, out var normalized);

            Assert.True(isValid);
            Assert.Equal(expected, normalized);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("0101234567")]      // too short
        [InlineData("010123456789")]    // too long
        [InlineData("01312345678")]     // 013 is not a mobile network
        [InlineData("02012345678")]     // landline
        [InlineData("abcdefghijk")]
        [InlineData("٠١٠١٢٣٤٥٦٧٨")]     // Arabic-Indic digits are not accepted
        [InlineData("01012345678' OR '1'='1")]
        [InlineData("01012345678; DROP TABLE accounts;--")]   // valid digits followed by other text
        [InlineData("0101234abc5678")]
        public void TryNormalize_rejects_invalid_numbers(string? typed)
        {
            var isValid = EgyptianPhone.TryNormalize(typed, out var normalized);

            Assert.False(isValid);
            Assert.Equal(string.Empty, normalized);
        }

        [Fact]
        public void Mask_hides_the_middle_digits()
        {
            Assert.Equal("010****5678", EgyptianPhone.Mask("01012345678"));
        }

        [Theory]
        [InlineData("")]
        [InlineData("123")]
        public void Mask_never_shows_a_very_short_value(string value)
        {
            Assert.Equal("****", EgyptianPhone.Mask(value));
        }

        [Theory]
        [InlineData("01012345678", true)]
        [InlineData(null, true)]         // "empty" is the job of [Required]
        [InlineData("12345", false)]
        [InlineData(12345, false)]       // not text at all
        public void EgyptianPhoneNumberAttribute_validates_request_values(object? value, bool expected)
        {
            var attribute = new EgyptianPhoneNumberAttribute();

            Assert.Equal(expected, attribute.IsValid(value));
        }
    }
}
