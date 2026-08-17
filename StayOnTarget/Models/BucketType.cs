using System.ComponentModel.DataAnnotations;

namespace StayOnTarget.Models;

public enum BucketType
{
    Standard = 0,             // Normal per-pay-period envelope
    [Display(Name = "Upfront Floor")]
    UpfrontFloor = 1,         // Immediate designated reserve baseline
    [Display(Name = "Accumulating Drawdown")]
    AccumulatingDrawdown = 2  // Cumulative balance / sinking fund with drawdown
}