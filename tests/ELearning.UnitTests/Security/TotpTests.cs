using ELearning.Services.Security;
using System.Text;

namespace ELearning.UnitTests.Security
{
    public class TotpTests
    {
        // The secret used by the official test values in RFC 6238 ("12345678901234567890").
        private static readonly string RfcSecret = Totp.ToBase32(Encoding.ASCII.GetBytes("12345678901234567890"));

        // Official test values from RFC 6238, appendix B (SHA-1). The RFC lists 8 digits;
        // a 6-digit code is the last 6 digits of the same number.
        [Theory]
        [InlineData(59L, "287082")]
        [InlineData(1111111109L, "081804")]
        [InlineData(1111111111L, "050471")]
        [InlineData(1234567890L, "005924")]
        [InlineData(2000000000L, "279037")]
        public void GetCode_matches_the_official_RFC_6238_values(long unixSeconds, string expectedCode)
        {
            var step = Totp.GetStep(DateTimeOffset.FromUnixTimeSeconds(unixSeconds));

            var code = Totp.GetCode(RfcSecret, step);

            Assert.Equal(expectedCode, code);
        }

        [Fact]
        public void FindMatchingStep_accepts_the_current_code()
        {
            var now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
            var secret = Totp.GenerateSecret();
            var code = Totp.GetCode(secret, Totp.GetStep(now));

            var matchedStep = Totp.FindMatchingStep(secret, code, now);

            Assert.Equal(Totp.GetStep(now), matchedStep);
        }

        [Theory]
        [InlineData(-30)]
        [InlineData(30)]
        public void FindMatchingStep_accepts_a_code_one_step_before_or_after(int secondsAway)
        {
            var now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
            var secret = Totp.GenerateSecret();
            var neighbourCode = Totp.GetCode(secret, Totp.GetStep(now.AddSeconds(secondsAway)));

            var matchedStep = Totp.FindMatchingStep(secret, neighbourCode, now);

            Assert.NotNull(matchedStep);
        }

        [Theory]
        [InlineData(-90)]
        [InlineData(90)]
        public void FindMatchingStep_rejects_a_code_that_is_too_old_or_too_new(int secondsAway)
        {
            var now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
            var secret = Totp.GenerateSecret();
            var farCode = Totp.GetCode(secret, Totp.GetStep(now.AddSeconds(secondsAway)));

            // In the very rare case that two steps give the same 6 digits, the test would be wrong, so skip it.
            var currentCodes = new[]
            {
                Totp.GetCode(secret, Totp.GetStep(now) - 1),
                Totp.GetCode(secret, Totp.GetStep(now)),
                Totp.GetCode(secret, Totp.GetStep(now) + 1),
            };
            if (currentCodes.Contains(farCode))
                return;

            var matchedStep = Totp.FindMatchingStep(secret, farCode, now);

            Assert.Null(matchedStep);
        }

        [Fact]
        public void FindMatchingStep_rejects_a_code_made_with_another_secret()
        {
            var now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);

            // Fixed secrets so the test always behaves the same way.
            var mySecret = Totp.ToBase32(Encoding.ASCII.GetBytes("AAAAAAAAAAAAAAAAAAAA"));
            var otherSecret = Totp.ToBase32(Encoding.ASCII.GetBytes("BBBBBBBBBBBBBBBBBBBB"));
            var otherCode = Totp.GetCode(otherSecret, Totp.GetStep(now));

            var matchedStep = Totp.FindMatchingStep(mySecret, otherCode, now);

            Assert.Null(matchedStep);
        }

        [Fact]
        public void Base32_text_can_be_turned_back_into_the_same_bytes()
        {
            var original = new byte[] { 0, 1, 2, 3, 250, 251, 252, 253, 254, 255, 42, 7, 99, 128, 64, 32, 16, 8, 4, 2 };

            var text = Totp.ToBase32(original);
            var back = Totp.FromBase32(text);

            Assert.Equal(original, back);
        }

        [Fact]
        public void GenerateSecret_gives_a_different_32_character_secret_every_time()
        {
            var first = Totp.GenerateSecret();
            var second = Totp.GenerateSecret();

            Assert.Equal(32, first.Length);
            Assert.NotEqual(first, second);
        }

        [Fact]
        public void BuildUri_contains_the_secret_and_escapes_the_names()
        {
            var uri = Totp.BuildUri("E Learning", "01012345678", "ABCDEF");

            Assert.StartsWith("otpauth://totp/E%20Learning%3A01012345678?", uri);
            Assert.Contains("secret=ABCDEF", uri);
            Assert.Contains("issuer=E%20Learning", uri);
        }

        [Fact]
        public void FromBase32_throws_for_text_that_is_not_Base32()
        {
            Assert.Throws<FormatException>(() => Totp.FromBase32("not-valid-1"));
        }
    }
}
