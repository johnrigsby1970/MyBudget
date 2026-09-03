using StayOnTarget.Models;

namespace StayOnTarget.Extensions;

public static class ValidationExtensions {
    public static List<string> GetValidationErrors(this Account account)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(account.Name))
            errors.Add("Account name is required.");

        if (string.IsNullOrWhiteSpace(account.BankName))
            errors.Add("Bank name is required.");

        if (account.IsLoanAccount)
        {
            var m = account.MortgageDetails;
            if (m == null)
            {
                errors.Add("Interest and statement details must be defined.");
            }
            else
            {
                if (m.InterestRate <= 0) errors.Add("Mortgage interest rate is required.");
                if (m.LoanPayment <= 0) errors.Add("Mortgage payment is required.");
                if (m.StatementDay <= 0) errors.Add("Mortgage statement day is required.");
            }
        }

        if (account.Type == AccountType.CreditCard)
        {
            var cc = account.CreditCardDetails;
            if (cc == null || cc.StatementDay <= 0)
                errors.Add("Credit card statement day is required.");

            if (account.AccountAprHistory == null)
                errors.Add("Credit card interest rate must be set.");
        }

        return errors;
    }
    
    public static List<string> GetValidationErrors(this Transaction transaction)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(transaction.Description))
            errors.Add("Description is required. Who is this transaction for?");

        if (!(transaction.AccountId > 0 || transaction.ToAccountId > 0))
            errors.Add("To or from account is required. What account is the money coming from or going to?");
        
        if (transaction.Amount==0)
            errors.Add("Amount is required. How much is involved in this transaction?");
        
        return errors;
    }
    
    public static List<string> GetValidationErrors(this Bill transaction)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(transaction.Name))
            errors.Add("Name is required. Who is this bill for?");

        if (!(transaction.AccountId > 0 || transaction.ToAccountId > 0))
            errors.Add("To or from account is required. What account is the money coming from or going to?");
        
        return errors;
    }
    
    public static List<string> GetValidationErrors(this BudgetBucket transaction)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(transaction.Name))
            errors.Add("Name is required. What best describes this budget item?");
        
        return errors;
    }
    
    public static List<string> GetValidationErrors(this Paycheck transaction)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(transaction.Name))
            errors.Add("Name is required. Where does this money come from?");

        if (!(transaction.AccountId > 0))
            errors.Add("Account is required. Where is this money deposited (Cash is a valid answer)?");
        
        return errors;
    }
    
    public static List<string> GetValidationErrors(this Category transaction)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(transaction.Name))
            errors.Add("Name is required.?");
        
        return errors;
    }
    
    public static List<string> GetValidationErrors(this SubCategory transaction)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(transaction.Name))
            errors.Add("Name is required.?");
        
        return errors;
    }
}