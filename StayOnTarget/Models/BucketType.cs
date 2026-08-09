namespace StayOnTarget.Models;

public enum BucketType
{
    Standard = 0,             // Normal per-pay-period envelope
    UpfrontFloor = 1,         // Immediate designated reserve baseline
    AccumulatingDrawdown = 2  // Cumulative balance / sinking fund with drawdown
}