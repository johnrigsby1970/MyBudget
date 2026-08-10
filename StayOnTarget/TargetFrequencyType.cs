namespace StayOnTarget;

public enum TargetFrequencyType
{
    PaycheckFrequency = 0, // <--- Tied directly to the associated paycheck's schedule
    Weekly = 1,
    BiWeekly = 2,
    SemiMonthly = 3,
    Monthly = 4,
    Quarterly = 5,
    Annual = 6,
    Custom = 7
}