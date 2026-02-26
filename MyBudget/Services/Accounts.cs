using Dapper;
using MyBudget.Models;

namespace MyBudget.Services;

public partial class BudgetService
{
    public IEnumerable<Account> GetAllAccounts()
    {
        using var conn = _db.GetConnection();
        var accounts = conn.Query<Account>("SELECT * FROM Accounts").ToList();
        foreach (var acc in accounts)
        {
            if (acc.Type == AccountType.Mortgage)
            {
                acc.MortgageDetails = conn.QueryFirstOrDefault<MortgageDetails>("SELECT * FROM MortgageDetails WHERE AccountId = @Id", new { acc.Id });
            }
        }
        return accounts;
    }

    public void UpsertAccount(Account account)
    {
        using var conn = _db.GetConnection();
        var accountParam = new
        {
            account.Id,
            account.Name,
            account.BankName,
            account.Balance,
            BalanceAsOf = account.BalanceAsOf.ToString("yyyy-MM-dd"),
            account.AnnualGrowthRate,
            account.IncludeInTotal,
            account.Type
        };

        if (account.Id == 0)
        {
            account.Id = conn.ExecuteScalar<int>(@"INSERT INTO Accounts (Name, BankName, Balance, BalanceAsOf, AnnualGrowthRate, IncludeInTotal, Type) 
                           VALUES (@Name, @BankName, @Balance, @BalanceAsOf, @AnnualGrowthRate, @IncludeInTotal, @Type);
                           SELECT last_insert_rowid();", accountParam);
        }
        else
        {
            conn.Execute(@"UPDATE Accounts SET Name=@Name, BankName=@BankName, Balance=@Balance, BalanceAsOf=@BalanceAsOf,
                           AnnualGrowthRate=@AnnualGrowthRate, IncludeInTotal=@IncludeInTotal, Type=@Type WHERE Id=@Id", accountParam);
        }

        if (account.Type == AccountType.Mortgage && account.MortgageDetails != null)
        {
            account.MortgageDetails.AccountId = account.Id;
            var mdParam = new
            {
                account.MortgageDetails.Id,
                account.MortgageDetails.AccountId,
                account.MortgageDetails.InterestRate,
                account.MortgageDetails.Escrow,
                account.MortgageDetails.MortgageInsurance,
                account.MortgageDetails.LoanPayment,
                PaymentDate = account.MortgageDetails.PaymentDate.ToString("yyyy-MM-dd")
            };
            if (account.MortgageDetails.Id == 0)
            {
                conn.Execute(@"INSERT INTO MortgageDetails (AccountId, InterestRate, Escrow, MortgageInsurance, LoanPayment, PaymentDate) 
                               VALUES (@AccountId, @InterestRate, @Escrow, @MortgageInsurance, @LoanPayment, @PaymentDate)", mdParam);
            }
            else
            {
                conn.Execute(@"UPDATE MortgageDetails SET InterestRate=@InterestRate, Escrow=@Escrow, 
                               MortgageInsurance=@MortgageInsurance, LoanPayment=@LoanPayment, PaymentDate=@PaymentDate WHERE Id=@Id", mdParam);
            }
        }
    }
    
    public void DeleteAccount(int id)
    {
        using var conn = _db.GetConnection();
        conn.Execute("DELETE FROM Accounts WHERE Id = @id", new { id });
    }

    public bool IsAccountInUse(int accountId)
    {
        using var conn = _db.GetConnection();
        
        // Check Bills (AccountId or ToAccountId)
        var billCount = conn.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM Bills WHERE AccountId = @accountId OR ToAccountId = @accountId", 
            new { accountId });
        if (billCount > 0) return true;

        // Check Buckets (AccountId)
        var bucketCount = conn.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM Buckets WHERE AccountId = @accountId", 
            new { accountId });
        if (bucketCount > 0) return true;

        // Check Transactions (AccountId or ToAccountId)
        var transactionCount = conn.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM Transactions WHERE AccountId = @accountId OR ToAccountId = @accountId", 
            new { accountId });
        if (transactionCount > 0) return true;

        return false;
    }
}