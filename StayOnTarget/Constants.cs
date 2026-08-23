using System.ComponentModel.DataAnnotations;

namespace StayOnTarget;

public static class Constants {
    public const string AppName = "StayOnTarget";
    public const string OpeningBalance = "Opening Balance";
}

public enum StrategyTaskType
{
    Investment = 0,             // Normal per-pay-period envelope
    [Display(Name = "Debt Payoff")]
    DebtPayoff = 1
}
