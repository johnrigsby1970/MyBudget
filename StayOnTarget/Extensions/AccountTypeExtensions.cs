using StayOnTarget.Models;

namespace StayOnTarget.Extensions;

public static class AccountTypeExtensions
{
    public static bool IsGrowthOrInvestmentAccount(this AccountType type)
    {
        return type switch
        {
            // Liquid / Yield-bearing Cash
            AccountType.Savings => true,
            AccountType.CD => true,

            // Taxable Growth & Brokerage
            AccountType.Investment => true,
            AccountType.Brokerage => true,

            // Retirement / Tax-Advantaged
            AccountType.IRA => true,
            AccountType.RothIRA => true,
            AccountType.Retirement401k => true,
            AccountType.Roth401k => true,
            AccountType.HSA => true,
            AccountType.CollegeFund => true,

            _ => false
        };
    }
}