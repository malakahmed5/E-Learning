using ELearning.Services.Security;
using ELearningHub.Core.Entities;

namespace ELearning.UnitTests.Security
{
    public class SecureCodeTests
    {
        [Fact]
        public void Generate_gives_12_characters_from_the_easy_to_read_alphabet()
        {
            var confusingCharacters = new[] { '0', 'O', '1', 'I', 'L' };

            for (var attempt = 0; attempt < 200; attempt++)
            {
                var code = SecureCode.Generate();

                Assert.Equal(12, code.Length);
                Assert.All(code, character => Assert.True(char.IsUpper(character) || char.IsDigit(character)));
                Assert.DoesNotContain(code, character => confusingCharacters.Contains(character));
            }
        }

        [Fact]
        public void Generate_does_not_repeat_codes()
        {
            var codes = new HashSet<string>();

            for (var attempt = 0; attempt < 5000; attempt++)
                codes.Add(SecureCode.Generate());

            Assert.Equal(5000, codes.Count);
        }

        [Fact]
        public void Format_splits_the_code_into_three_groups()
        {
            Assert.Equal("K7QM-2XRF-9TPA", SecureCode.Format("K7QM2XRF9TPA"));
        }

        [Theory]
        [InlineData("K7QM-2XRF-9TPA", "K7QM2XRF9TPA")]
        [InlineData("k7qm 2xrf 9tpa", "K7QM2XRF9TPA")]
        [InlineData("  k7qm2xrf9tpa  ", "K7QM2XRF9TPA")]
        [InlineData("", "")]
        [InlineData(null, "")]
        public void Normalize_cleans_what_the_user_typed(string? typed, string expected)
        {
            Assert.Equal(expected, SecureCode.Normalize(typed));
        }

        [Fact]
        public void Hash_is_the_same_for_the_same_text_and_different_for_other_text()
        {
            var first = SecureCode.Hash("K7QM2XRF9TPA");
            var again = SecureCode.Hash("K7QM2XRF9TPA");
            var other = SecureCode.Hash("K7QM2XRF9TPB");

            Assert.Equal(64, first.Length);
            Assert.Equal(first, again);
            Assert.NotEqual(first, other);
        }

        [Fact]
        public void AccountInvite_stores_only_the_hash_and_sets_the_expiry()
        {
            var account = new Account();
            var now = new DateTime(2027, 9, 1, 10, 0, 0, DateTimeKind.Utc);

            var invite = AccountInvite.Start(account, now, validDays: 7);

            var typedByUser = SecureCode.Normalize(invite.Code);
            Assert.Equal(SecureCode.Hash(typedByUser), account.InviteCodeHash);
            Assert.NotEqual(typedByUser, account.InviteCodeHash);
            Assert.Equal(now.AddDays(7), account.InviteExpiresAt);
            Assert.Equal(now.AddDays(7), invite.ExpiresAt);
        }
    }
}
