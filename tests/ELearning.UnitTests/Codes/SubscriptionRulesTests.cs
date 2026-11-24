using ELearning.Core.Entities.Subscriptions;
using ELearning.Services.Codes;
using ELearning.Services.Options;
using ELearning.Services.Security;
using ELearning.Shared.DTOs.Codes;
using ELearningHub.Core.Entities;

namespace ELearning.UnitTests.Codes
{
    public class SubscriptionRulesTests
    {
        private static readonly DateTime Now = new(2027, 9, 10, 12, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void A_student_who_never_redeemed_a_code_is_not_activated()
        {
            var status = SubscriptionRules.GetStatus(accessEndsAt: null, latestPeriodType: null, Now, expiringSoonDays: 3);

            Assert.Equal(SubscriptionState.NotActivated, status.Status);
            Assert.Equal(0, status.DaysLeft);
            Assert.False(status.ExpiringSoon);
        }

        [Fact]
        public void Days_left_are_counted_and_the_free_week_is_shown_as_trial()
        {
            var status = SubscriptionRules.GetStatus(Now.AddDays(7), PeriodType.Trial, Now, 3);

            Assert.Equal(SubscriptionState.Trial, status.Status);
            Assert.Equal(7, status.DaysLeft);
            Assert.False(status.ExpiringSoon);
        }

        [Fact]
        public void Paid_days_are_shown_as_active()
        {
            var status = SubscriptionRules.GetStatus(Now.AddDays(30), PeriodType.Paid, Now, 3);

            Assert.Equal(SubscriptionState.Active, status.Status);
            Assert.Equal(30, status.DaysLeft);
        }

        [Theory]
        [InlineData(72, true)]     // exactly 3 days left: warn
        [InlineData(73, false)]    // a little more than 3 days: no warning yet
        [InlineData(1, true)]
        public void The_warning_starts_in_the_last_three_days(int hoursLeft, bool expectedWarning)
        {
            var status = SubscriptionRules.GetStatus(Now.AddHours(hoursLeft), PeriodType.Paid, Now, 3);

            Assert.Equal(expectedWarning, status.ExpiringSoon);
        }

        [Fact]
        public void A_few_hours_left_still_count_as_one_day()
        {
            var status = SubscriptionRules.GetStatus(Now.AddHours(2), PeriodType.Paid, Now, 3);

            Assert.Equal(1, status.DaysLeft);
        }

        [Theory]
        [InlineData(0)]      // ends exactly now
        [InlineData(-1)]
        [InlineData(-500)]
        public void When_the_end_time_has_come_the_subscription_is_expired(int hoursFromNow)
        {
            var status = SubscriptionRules.GetStatus(Now.AddHours(hoursFromNow), PeriodType.Paid, Now, 3);

            Assert.Equal(SubscriptionState.Expired, status.Status);
            Assert.Equal(0, status.DaysLeft);
            Assert.False(status.ExpiringSoon);
        }
    }

    public class AccountRulesTests
    {
        [Theory]
        [InlineData("Teacher123", true)]
        [InlineData("كلمةسر123", true)]      // Arabic letters count as letters
        [InlineData("short1", false)]
        [InlineData("onlyletters", false)]
        [InlineData("12345678", false)]
        [InlineData("", false)]
        public void Password_rule(string password, bool expected)
        {
            Assert.Equal(expected, PasswordRules.IsStrongEnough(password));
        }

        [Fact]
        public void The_account_locks_on_the_fifth_wrong_try_and_the_counter_starts_again()
        {
            var account = new Account();
            var options = new AuthOptions { MaxFailedAttempts = 5, LockoutMinutes = 15 };
            var now = new DateTime(2027, 9, 10, 12, 0, 0, DateTimeKind.Utc);

            for (var attempt = 1; attempt <= 4; attempt++)
                AccountLockout.RegisterFailure(account, options, now);

            Assert.False(AccountLockout.IsLocked(account, now));
            Assert.Equal(4, account.AccessFailedCount);

            AccountLockout.RegisterFailure(account, options, now);

            Assert.True(AccountLockout.IsLocked(account, now));
            Assert.Equal(0, account.AccessFailedCount);
            Assert.True(AccountLockout.IsLocked(account, now.AddMinutes(14)));
            Assert.False(AccountLockout.IsLocked(account, now.AddMinutes(15)));
        }
    }
}
