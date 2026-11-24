namespace ELearning.Core.Entities.Subscriptions
{
    public enum PeriodType
    {
        Trial = 1,
        Paid = 2,
    }

    public enum PeriodSource
    {
        ActivationCode = 1,
        SubscriptionCode = 2,
        // Later: Paymob = 3
    }
}
