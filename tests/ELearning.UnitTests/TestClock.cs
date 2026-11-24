namespace ELearning.UnitTests
{
    // A clock the test controls. Lets a test "jump" forward in time without waiting.
    public class TestClock : TimeProvider
    {
        private DateTimeOffset _now;

        public TestClock(DateTimeOffset now)
        {
            _now = now;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _now;
        }

        public void Advance(TimeSpan amount)
        {
            _now = _now.Add(amount);
        }
    }
}
