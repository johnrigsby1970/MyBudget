namespace StayOnTarget;

public static class Constants {
    public const string OpeningBalance = "Opening Balance";
}

public enum StrategyTaskType
{
    Investment = 0,             // Normal per-pay-period envelope
    DebtPayoff = 1
}
