namespace StayOnTarget.Models;

public enum TransactionType
{
    Income,
    Expense,
    LoanPayment,
    Investment
}

public enum Frequency
{
    Monthly,
    Yearly,
    BiWeekly,
    Weekly,
    Once
}

public enum AccountType
{
    Checking,
    Savings,
    Investment,
    CD,
    Retirement401k,
    Brokerage,
    Mortgage,
    PersonalLoan,
    CreditCard,
    RealEstate,
    AppreciatingAsset,
    Auto,
    CollegeFund,// 529 / Coverdell
    Cash,
    IRA,
    RothIRA,
    Roth401k,
    HSA,
    FDA,
    Pension,
    DigitalAsset,
    OtherAsset,
    OtherLiability,
    HELOC,
    StudentLoan,
    RentalProperty,
    Business
}
