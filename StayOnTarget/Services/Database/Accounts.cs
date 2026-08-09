using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;
using StayOnTarget.Models;

namespace StayOnTarget.Services;

public partial class BudgetService {
    public async Task<IEnumerable<Account>> GetAllAccountsAsync(bool includeArchived = false) {
        await using var conn = _db.GetConnection();
        var accounts =
            (await conn.QueryAsync<Account>("SELECT * FROM Accounts WHERE IsArchived=0 OR @includeArchived=1",
                new { includeArchived = (includeArchived ? 1 : 0) })).ToList();
        foreach (var acc in accounts) {
            if (acc.IsLoanAccount) {
                acc.MortgageDetails =
                    await conn.QueryFirstOrDefaultAsync<MortgageDetails>(
                        "SELECT * FROM MortgageDetails WHERE AccountId = @Id", new { acc.Id });
            }

            if (acc.Type == AccountType.CreditCard) {
                acc.CreditCardDetails =
                    await conn.QueryFirstOrDefaultAsync<CreditCardDetails>(
                        "SELECT * FROM CreditCardDetails WHERE AccountId = @Id", new { acc.Id });
                acc.AccountAprHistory = (await conn.QueryAsync<AccountAprHistory>(
                    "SELECT * FROM AccountAprHistory WHERE AccountId = @Id", new { acc.Id })).ToList();
            }
        }

        accounts.ForEach(x => { x.Balance = 0; });
        return accounts;
    }

    public async Task<IEnumerable<Account>> GetAllAccountsAsOfAsync(DateTime asOfDate, bool includeArchived = false,
        SqliteConnection? cn = null, IDbTransaction? tx = null) {
        bool isLocalConn = cn == null;
        var conn = cn ?? _db.GetConnection();

        try {
            // Ensure local connection is open before executing queries
            if (isLocalConn && conn.State != ConnectionState.Open) {
                await conn.OpenAsync();
            }

            var accounts =
                (await conn.QueryAsync<Account>("SELECT * FROM Accounts WHERE IsArchived=0 OR @includeArchived=1",
                    new { includeArchived = (includeArchived ? 1 : 0) }, tx)).ToList();
            foreach (var acc in accounts) {
                if (acc.IsLoanAccount) {
                    acc.MortgageDetails =
                        await conn.QueryFirstOrDefaultAsync<MortgageDetails>(
                            "SELECT * FROM MortgageDetails WHERE AccountId = @Id", new { acc.Id }, tx);
                }

                if (acc.Type == AccountType.CreditCard) {
                    acc.CreditCardDetails =
                        await conn.QueryFirstOrDefaultAsync<CreditCardDetails>(
                            "SELECT * FROM CreditCardDetails WHERE AccountId = @Id", new { acc.Id }, tx);
                    acc.AccountAprHistory = (await conn.QueryAsync<AccountAprHistory>(
                        "SELECT * FROM AccountAprHistory WHERE AccountId = @Id", new { acc.Id }, tx)).ToList();
                }
            }

            //Because our projection massages the paycheck date to be that of its expected date, we will do the same here
            string query = """
                           SELECT 
                               t.AccountId AS Id, 
                               ROUND(
                                   SUM(
                                       CASE 
                                           -- If it's principal-only, the entire amount goes to the balance
                                           -- If the amount is negative, it is not a payment, its interest or some adjustment
                                           WHEN t.IsPrincipalOnly = 1 OR t.Amount < 0 THEN t.Amount
                                           -- Otherwise, subtract the escrow amount active at the time of the transaction
                                           ELSE t.Amount - COALESCE(
                                               (
                                                   SELECT md.Escrow 
                                                   FROM MortgageDetails md
                                                   WHERE md.AccountId = t.AccountId -- Ensures we match the specific account
                                                     AND md.PaymentDate <= COALESCE(t.PaycheckOccurrenceDate, t.TransactionDate)
                                                   ORDER BY md.PaymentDate DESC
                                                   LIMIT 1
                                               ), 
                                               0
                                           )
                                       END
                                   ), 
                                   2
                               ) AS Balance 
                           FROM Transactions t
                           WHERE (date(t.TransactionDate) <= @asOfDate AND t.PayCheckId IS NULL) 
                              OR (date(t.PaycheckOccurrenceDate) <= @asOfDate AND t.PayCheckId IS NOT NULL) 
                           GROUP BY t.AccountId;
                           """;

            var accountBalances =
                (await conn.QueryAsync<Account>(query, new { asOfDate = asOfDate.ToString("yyyy-MM-dd") }, tx))
                .ToList();

            accounts.ForEach(x => { x.Balance = 0; });
            foreach (var account in accounts) {
                if (accountBalances.Any(x => x.Id == account.Id)) {
                    account.Balance = accountBalances.FirstOrDefault(x => x.Id == account.Id)!.Balance;
                    account.BalanceAsOf = asOfDate;
                }
            }

            return accounts;
        }
        finally {
            // 3. Only dispose connection if created locally inside this method call
            if (isLocalConn) {
                await conn.DisposeAsync();
            }
        }
    }

    public async Task<int> UpsertAccountAsync(Account account) {
        await using var conn = _db.GetConnection();
        var accountParam = new {
            account.Id,
            account.Name,
            account.BankName,
            account.Balance,
            BalanceAsOf = account.BalanceAsOf.ToString("yyyy-MM-dd"),
            account.AnnualGrowthRate,
            account.IncludeInTotal,
            account.Type,
            account.HexColor,
            account.IsPrimary
        };

        if (account.Id == 0) {
            //balance is not being saved here
            account.Id = await conn.ExecuteScalarAsync<int>(
                @"INSERT INTO Accounts (Name, BankName, Balance,  BalanceAsOf, AnnualGrowthRate, IncludeInTotal, Type, HexColor, IsPrimary) 
                           VALUES (@Name, @BankName, 0, @BalanceAsOf, @AnnualGrowthRate, @IncludeInTotal, @Type, @HexColor, @IsPrimary);
                           SELECT last_insert_rowid();", accountParam);
        }
        else {
            await conn.ExecuteAsync(@"UPDATE Accounts SET Name=@Name, BankName=@BankName, BalanceAsOf=@BalanceAsOf,
                           AnnualGrowthRate=@AnnualGrowthRate, IncludeInTotal=@IncludeInTotal, Type=@Type, HexColor=@HexColor, IsPrimary=@IsPrimary WHERE Id=@Id",
                accountParam);
        }

        if ((account.IsLoanAccount) && account.MortgageDetails != null) {
            account.MortgageDetails.AccountId = account.Id;
            var mdParam = new {
                account.MortgageDetails.Id,
                account.MortgageDetails.AccountId,
                account.MortgageDetails.InterestRate,
                account.MortgageDetails.Escrow,
                account.MortgageDetails.MortgageInsurance,
                account.MortgageDetails.LoanPayment,
                PaymentDate = account.MortgageDetails.PaymentDate.ToString("yyyy-MM-dd"),
                account.MortgageDetails.StatementDay
            };
            if (account.MortgageDetails.Id == 0) {
                await conn.ExecuteAsync(
                    @"INSERT INTO MortgageDetails (AccountId, InterestRate, Escrow, MortgageInsurance, LoanPayment, PaymentDate, StatementDay) 
                           VALUES (@AccountId, @InterestRate, @Escrow, @MortgageInsurance, @LoanPayment, @PaymentDate, @StatementDay)",
                    mdParam);
            }
            else {
                await conn.ExecuteAsync(@"UPDATE MortgageDetails SET InterestRate=@InterestRate, Escrow=@Escrow,
                           MortgageInsurance=@MortgageInsurance, LoanPayment=@LoanPayment, PaymentDate=@PaymentDate, StatementDay=@StatementDay WHERE Id=@Id",
                    mdParam);
            }
        }

        if (account.Type == AccountType.CreditCard && account.CreditCardDetails != null) {
            account.CreditCardDetails.AccountId = account.Id;
            var ccdParam = new {
                account.CreditCardDetails.Id,
                account.CreditCardDetails.AccountId,
                account.CreditCardDetails.StatementDay,
                account.CreditCardDetails.DueDateOffset,
                account.CreditCardDetails.GraceActive,
                account.CreditCardDetails.MinPayFloor,
                PayPreviousMonthBalanceInFull = account.CreditCardDetails.PayPreviousMonthBalanceInFull ? 1 : 0
            };
            if (account.CreditCardDetails.Id == 0) {
                await conn.ExecuteAsync(
                    @"INSERT INTO CreditCardDetails (AccountId, StatementDay, DueDateOffset, GraceActive, MinPayFloor, PayPreviousMonthBalanceInFull) 
                               VALUES (@AccountId, @StatementDay, @DueDateOffset, @GraceActive, @MinPayFloor, @PayPreviousMonthBalanceInFull)",
                    ccdParam);
            }
            else {
                await conn.ExecuteAsync(
                    @"UPDATE CreditCardDetails SET StatementDay=@StatementDay, DueDateOffset=@DueDateOffset, GraceActive=@GraceActive, MinPayFloor=@MinPayFloor,
                               PayPreviousMonthBalanceInFull=@PayPreviousMonthBalanceInFull WHERE Id=@Id", ccdParam);
            }

            if (account.AccountAprHistory != null) {
                foreach (var aah in account.AccountAprHistory) {
                    aah.AccountId = account.Id;
                    await UpsertAccountAprHistoryAsync(aah);
                }
            }
        }

        return account.Id;
    }

    public async Task ArchiveAccountAsync(int id) {
        await using var conn = _db.GetConnection();
        await conn.ExecuteAsync(@"UPDATE Accounts SET IsArchived=1 WHERE Id=@id", new { id });
    }

    public async Task UnArchiveAccountAsync(int id) {
        await using var conn = _db.GetConnection();
        await conn.ExecuteAsync(@"UPDATE Accounts SET IsArchived=0 WHERE Id=@id", new { id });
    }

    public async Task DeleteAccountAsync(int id) {
        if (!await IsAccountInUseAsync(id)) {
            await using var conn = _db.GetConnection();
            await conn.ExecuteAsync("DELETE FROM Transactions WHERE AccountId = @id", new { id });
            await conn.ExecuteAsync("DELETE FROM AccountSnapshots WHERE AccountId = @id", new { id });
            await conn.ExecuteAsync("DELETE FROM AccountReconciliations WHERE AccountId = @id", new { id });
            await conn.ExecuteAsync("DELETE FROM AccountAprHistory WHERE AccountId = @id", new { id });
            await conn.ExecuteAsync("UPDATE Bills Set ToAccountId=null WHERE ToAccountId = @id", new { id });
            await conn.ExecuteAsync("UPDATE Bills Set AccountId=null WHERE AccountId = @id", new { id });
            await conn.ExecuteAsync("UPDATE Paychecks Set AccountId=null WHERE AccountId = @id", new { id });
            await conn.ExecuteAsync("DELETE FROM MortgageDetails WHERE AccountId = @id", new { id });
            await conn.ExecuteAsync("DELETE FROM CreditCardDetails WHERE AccountId = @id", new { id });
            await conn.ExecuteAsync("DELETE FROM Accounts WHERE Id = @id", new { id });
        }
        else {
            await using var conn = _db.GetConnection();
            await conn.ExecuteAsync(@"UPDATE Accounts SET IsArchived=1 WHERE Id=@id", new { id });
        }
    }

    public async Task<bool> IsAccountInUseAsync(int accountId) {
        await using var conn = _db.GetConnection();

        // Check Bills (AccountId or ToAccountId)
        var bills = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Bills WHERE AccountId = @accountId OR ToAccountId = @accountId",
            new { accountId });
        if (bills > 0) return true;

        // Check Buckets (AccountId)
        var buckets = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Buckets WHERE AccountId = @accountId",
            new { accountId });
        if (buckets > 0) return true;

        // Check Paychecks (AccountId)
        var paychecks = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Paychecks WHERE AccountId = @accountId",
            new { accountId });
        if (paychecks > 0) return true;

        // Check Transactions (AccountId without concern for initial balance)
        var transactions = await conn.ExecuteScalarAsync<int>(
            $"SELECT COUNT(*) FROM Transactions WHERE AccountId = @accountId and not Description=@description",
            new { accountId, description = Constants.OpeningBalance });
        if (transactions > 0) return true;

        // var accountSnapshots = await conn.ExecuteScalarAsync<int>(
        //     "SELECT COUNT(*) FROM AccountSnapshots WHERE AccountId = @accountId ", 
        //     new { accountId });
        // if (accountSnapshots > 0) return true;
        //
        // var accountAprHistory = await conn.ExecuteScalarAsync<int>(
        //     "SELECT COUNT(*) FROM AccountAprHistory WHERE AccountId = @accountId ", 
        //     new { accountId });
        // if (accountAprHistory > 0) return true;
        //
        // var accountReconciliations = await conn.ExecuteScalarAsync<int>(
        //     "SELECT COUNT(*) FROM AccountReconciliations WHERE AccountId = @accountId ", 
        //     new { accountId });
        // if (accountReconciliations > 0) return true;
        //
        // var mortgageDetails = await conn.ExecuteScalarAsync<int>(
        //     "SELECT COUNT(*) FROM MortgageDetails WHERE AccountId = @accountId ", 
        //     new { accountId });
        // if (mortgageDetails > 0) return true;
        //
        // var creditCardDetails = await conn.ExecuteScalarAsync<int>(
        //     "SELECT COUNT(*) FROM CreditCardDetails WHERE AccountId = @accountId ", 
        //     new { accountId });
        // if (creditCardDetails > 0) return true;

        return false;
    }

    public async Task<(bool hasTransactions, decimal? openingBalance, DateTime? openingBalanceDate)>
        GetAccountBalanceOpeningStateAsync(int accountId) {
        await using var conn = _db.GetConnection();

        var hasTransactions = false;
        decimal? openingBalance = null;
        DateTime? openingBalanceDate = null;

        var transactions = await conn.ExecuteScalarAsync<int>(
            $"SELECT COUNT(*) FROM Transactions WHERE AccountId = @accountId AND NOT Description=@description",
            new { accountId, description = Constants.OpeningBalance });

        hasTransactions = transactions > 0;

        var accounts =
            (await conn.QueryAsync<dynamic>(
                $"SELECT AccountId, Amount, TransactionDate FROM Transactions WHERE Description=@description AND AccountId=@accountId",
                new { accountId, description = Constants.OpeningBalance })).ToList();
        if (accounts.Count > 0) {
            var account = accounts.First();
            openingBalance = (decimal)account.Amount;
            openingBalanceDate = Convert.ToDateTime(account.TransactionDate);
        }

        return (hasTransactions, openingBalance, openingBalanceDate);
    }

    public async Task<DateTime?> GetOldestOpeningBalanceAsync() {
        await using var conn = _db.GetConnection();

        const string sql = @"
        SELECT MIN(TransactionDate) 
        FROM Transactions 
        WHERE Description = @Description";

        var openingBalanceAsOf = await conn.ExecuteScalarAsync<DateTime?>(
            sql,
            new { Description = Constants.OpeningBalance });

        return openingBalanceAsOf;
    }

    public async Task<DateTime?> GetOldestTransactionAsync() {
        await using var conn = _db.GetConnection();

        const string sql = @"
        SELECT MIN(TransactionDate) 
        FROM Transactions";

        var openingBalanceAsOf = await conn.ExecuteScalarAsync<DateTime?>(
            sql);

        return openingBalanceAsOf;
    }
}