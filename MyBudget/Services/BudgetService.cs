using Dapper;
using MyBudget.Data;
using MyBudget.Models;
using System.Collections.Generic;
using System.Linq;

namespace MyBudget.Services;

public class BudgetService
{
    private readonly DatabaseContext _db;

    public BudgetService()
    {
        _db = new DatabaseContext();
    }

    public IEnumerable<Bill> GetAllBills()
    {
        using var conn = _db.GetConnection();
        return conn.Query<Bill>("SELECT * FROM Bills WHERE IsActive = 1");
    }

    public void UpsertBill(Bill bill)
    {
        using var conn = _db.GetConnection();
        var param = new
        {
            bill.Id,
            bill.Name,
            bill.ExpectedAmount,
            bill.Frequency,
            bill.DueDay,
            bill.AccountId,
            bill.ToAccountId,
            NextDueDate = bill.NextDueDate?.ToString("yyyy-MM-dd"),
            bill.Category,
            bill.IsActive
        };
        if (bill.Id == 0)
        {
            conn.Execute(@"INSERT INTO Bills (Name, ExpectedAmount, Frequency, DueDay, AccountId, ToAccountId, NextDueDate, Category, IsActive) 
                           VALUES (@Name, @ExpectedAmount, @Frequency, @DueDay, @AccountId, @ToAccountId, @NextDueDate, @Category, @IsActive)", param);
        }
        else
        {
            conn.Execute(@"UPDATE Bills SET Name=@Name, ExpectedAmount=@ExpectedAmount, Frequency=@Frequency, 
                           DueDay=@DueDay, AccountId=@AccountId, ToAccountId=@ToAccountId, NextDueDate=@NextDueDate, Category=@Category, IsActive=@IsActive WHERE Id=@Id", param);
        }
    }

    public IEnumerable<PeriodBill> GetPeriodBills(DateTime periodDate)
    {
        using var conn = _db.GetConnection();
        return conn.Query<PeriodBill>(@"
            SELECT pb.*, b.Name as BillName 
            FROM PeriodBills pb 
            JOIN Bills b ON pb.BillId = b.Id 
            WHERE pb.PeriodDate = @periodDate", new { periodDate = periodDate.ToString("yyyy-MM-dd") });
    }

    public IEnumerable<PeriodBill> GetAllPeriodBills()
    {
        using var conn = _db.GetConnection();
        return conn.Query<PeriodBill>(@"
            SELECT pb.*, b.Name as BillName 
            FROM PeriodBills pb 
            JOIN Bills b ON pb.BillId = b.Id");
    }

    public void UpsertPeriodBill(PeriodBill pb)
    {
        using var conn = _db.GetConnection();
        var param = new
        {
            pb.Id,
            pb.BillId,
            PeriodDate = pb.PeriodDate.ToString("yyyy-MM-dd"),
            DueDate = pb.DueDate.ToString("yyyy-MM-dd"),
            pb.ActualAmount,
            pb.IsPaid,
            FitId = pb.FitId.ToString()
        };
        if (pb.Id == 0)
        {
            conn.Execute(@"INSERT INTO PeriodBills (BillId, PeriodDate, DueDate, ActualAmount, IsPaid, FitId) 
                           VALUES (@BillId, @PeriodDate, @DueDate, @ActualAmount, @IsPaid, @FitId)", param);
        }
        else
        {
            conn.Execute(@"UPDATE PeriodBills SET BillId=@BillId, PeriodDate=@PeriodDate, DueDate=@DueDate, 
                           ActualAmount=@ActualAmount, IsPaid=@IsPaid WHERE Id=@Id", param);
        }
    }

    public IEnumerable<Paycheck> GetAllPaychecks()
    {
        using var conn = _db.GetConnection();
        return conn.Query<Paycheck>("SELECT * FROM Paychecks");
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

        // Check AdHocTransactions (AccountId or ToAccountId)
        var adHocCount = conn.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM AdHocTransactions WHERE AccountId = @accountId OR ToAccountId = @accountId", 
            new { accountId });
        if (adHocCount > 0) return true;

        return false;
    }

    public void UpsertPaycheck(Paycheck paycheck)
    {
        using var conn = _db.GetConnection();
        var param = new
        {
            paycheck.Id,
            paycheck.Name,
            paycheck.ExpectedAmount,
            paycheck.Frequency,
            StartDate = paycheck.StartDate.ToString("yyyy-MM-dd"),
            EndDate = paycheck.EndDate?.ToString("yyyy-MM-dd"),
            paycheck.AccountId,
            paycheck.IsBalanced
        };
        if (paycheck.Id == 0)
        {
            conn.Execute(@"INSERT INTO Paychecks (Name, ExpectedAmount, Frequency, StartDate, EndDate, AccountId, IsBalanced) 
                           VALUES (@Name, @ExpectedAmount, @Frequency, @StartDate, @EndDate, @AccountId, @IsBalanced)", param);
        }
        else
        {
            conn.Execute(@"UPDATE Paychecks SET Name=@Name, ExpectedAmount=@ExpectedAmount, Frequency=@Frequency, 
                           StartDate=@StartDate, EndDate=@EndDate, AccountId=@AccountId, IsBalanced=@IsBalanced WHERE Id=@Id", param);
        }
    }


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

    public void DeletePeriodBill(int id)
    {
        using var conn = _db.GetConnection();
        conn.Execute("DELETE FROM PeriodBills WHERE Id = @id AND IsPaid = 0", new { id });
    }

    public void DeleteBill(int id)
    {
        using var conn = _db.GetConnection();
        conn.Execute("UPDATE Bills SET IsActive = 0 WHERE Id = @id", new { id });
    }

    // Bucket Operations
    public IEnumerable<BudgetBucket> GetAllBuckets()
    {
        using var conn = _db.GetConnection();
        return conn.Query<BudgetBucket>("SELECT * FROM Buckets");
    }

    public void UpsertBucket(BudgetBucket bucket)
    {
        using var conn = _db.GetConnection();
        if (bucket.Id == 0)
        {
            conn.Execute(@"INSERT INTO Buckets (Name, ExpectedAmount, AccountId) 
                           VALUES (@Name, @ExpectedAmount, @AccountId)", bucket);
        }
        else
        {
            conn.Execute(@"UPDATE Buckets SET Name=@Name, ExpectedAmount=@ExpectedAmount, AccountId=@AccountId WHERE Id=@Id", bucket);
        }
    }

    public void DeleteBucket(int id)
    {
        using var conn = _db.GetConnection();
        conn.Execute("DELETE FROM Buckets WHERE Id = @id", new { id });
    }

    public IEnumerable<PeriodBucket> GetPeriodBuckets(DateTime periodDate)
    {
        using var conn = _db.GetConnection();
        return conn.Query<PeriodBucket>(@"
            SELECT pb.*, b.Name as BucketName 
            FROM PeriodBuckets pb 
            JOIN Buckets b ON pb.BucketId = b.Id 
            WHERE pb.PeriodDate = @periodDate", new { periodDate = periodDate.ToString("yyyy-MM-dd") });
    }

    public IEnumerable<PeriodBucket> GetAllPeriodBuckets()
    {
        using var conn = _db.GetConnection();
        return conn.Query<PeriodBucket>(@"
            SELECT pb.*, b.Name as BucketName 
            FROM PeriodBuckets pb 
            JOIN Buckets b ON pb.BucketId = b.Id");
    }

    public void UpsertPeriodBucket(PeriodBucket pb)
    {
        using var conn = _db.GetConnection();
        var param = new
        {
            pb.Id,
            pb.BucketId,
            PeriodDate = pb.PeriodDate.ToString("yyyy-MM-dd"),
            pb.ActualAmount,
            pb.IsPaid,
            FitId = pb.FitId.ToString()
        };
        if (pb.Id == 0)
        {
            conn.Execute(@"INSERT INTO PeriodBuckets (BucketId, PeriodDate, ActualAmount, IsPaid, FitId) 
                           VALUES (@BucketId, @PeriodDate, @ActualAmount, @IsPaid, @FitId)", param);
        }
        else
        {
            conn.Execute(@"UPDATE PeriodBuckets SET BucketId=@BucketId, PeriodDate=@PeriodDate, 
                           ActualAmount=@ActualAmount, IsPaid=@IsPaid WHERE Id=@Id", param);
        }
    }

    public void DeletePeriodBucket(int id)
    {
        using var conn = _db.GetConnection();
        conn.Execute("DELETE FROM PeriodBuckets WHERE Id = @id AND IsPaid = 0", new { id });
    }
    public IEnumerable<AdHocTransaction> GetAdHocTransactions(DateTime periodDate)
    {
        using var conn = _db.GetConnection();
        return conn.Query<AdHocTransaction>(@"
            SELECT t.*, a1.Name as AccountName, a2.Name as ToAccountName 
            FROM AdHocTransactions t
            LEFT JOIN Accounts a1 ON t.AccountId = a1.Id
            LEFT JOIN Accounts a2 ON t.ToAccountId = a2.Id
            WHERE t.PeriodDate = @periodDate", new { periodDate = periodDate.ToString("yyyy-MM-dd") });
    }

    public IEnumerable<AdHocTransaction> GetAllAdHocTransactions()
    {
        using var conn = _db.GetConnection();
        return conn.Query<AdHocTransaction>("SELECT * FROM AdHocTransactions");
    }

    public void UpsertAdHocTransaction(AdHocTransaction t)
    {
        using var conn = _db.GetConnection();
        var param = new
        {
            t.Id,
            t.Description,
            t.Amount,
            Date = t.Date.ToString("yyyy-MM-dd"),
            t.AccountId,
            t.ToAccountId,
            t.BucketId,
            PeriodDate = t.PeriodDate.ToString("yyyy-MM-dd"),
            t.IsPrincipalOnly,
            FitId = t.FitId.ToString(),
            t.PaycheckId,
            PaycheckOccurrenceDate = t.PaycheckOccurrenceDate?.ToString("yyyy-MM-dd")
        };
        if (t.Id == 0)
        {
            conn.Execute(@"INSERT INTO AdHocTransactions (Description, Amount, Date, AccountId, ToAccountId, BucketId, PeriodDate, IsPrincipalOnly, FitId, PaycheckId, PaycheckOccurrenceDate) 
                           VALUES (@Description, @Amount, @Date, @AccountId, @ToAccountId, @BucketId, @PeriodDate, @IsPrincipalOnly, @FitId, @PaycheckId, @PaycheckOccurrenceDate)", param);
        }
        else
        {
            conn.Execute(@"UPDATE AdHocTransactions SET Description=@Description, Amount=@Amount, Date=@Date, 
                           AccountId=@AccountId, ToAccountId=@ToAccountId, BucketId=@BucketId, PeriodDate=@PeriodDate, IsPrincipalOnly=@IsPrincipalOnly, PaycheckId=@PaycheckId, PaycheckOccurrenceDate=@PaycheckOccurrenceDate WHERE Id=@Id", param);
        }
    }

    public void DeleteAdHocTransaction(int id)
    {
        using var conn = _db.GetConnection();
        conn.Execute("DELETE FROM AdHocTransactions WHERE Id = @id", new { id });
    }
}
