using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;
using StayOnTarget.Models;
using Serilog;

namespace StayOnTarget.Services;

public partial class BudgetService {
    public async Task<IEnumerable<Account>> GetAllAccountsAsync(bool includeArchived = false) {
        try {
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
        catch (Exception ex) {
            Log.Error(ex, "Error getting all accounts[cite: 22].");
            return Enumerable.Empty<Account>();
        }
    }

    public async Task<IEnumerable<Account>> GetAllAccountsAsOfAsync(DateTime asOfDate, bool includeArchived = false,
        SqliteConnection? cn = null, IDbTransaction? tx = null) {
        try {
            bool isLocalConn = cn == null;
            var conn = cn ?? _db.GetConnection();

            try {
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

                string query = """
                               SELECT 
                                   t.AccountId AS Id, 
                                   ROUND(
                                       SUM(
                                           CASE 
                                               WHEN t.IsPrincipalOnly = 1 OR t.Amount < 0 THEN t.Amount
                                               ELSE t.Amount - COALESCE(
                                                   (
                                                       SELECT md.Escrow 
                                                       FROM MortgageDetails md
                                                       WHERE md.AccountId = t.AccountId 
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
                if (isLocalConn) {
                    await conn.DisposeAsync();
                }
            }
        }
        catch (Exception ex) {
            Log.Error(ex, "Error getting accounts as of date {AsOfDate}[cite: 22].", asOfDate);
            return Enumerable.Empty<Account>();
        }
    }

    public async Task<int> UpsertAccountAsync(Account account) {
        try {
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
        catch (Exception ex) {
            Log.Error(ex, "Error upserting account with ID {AccountId}[cite: 22].", account.Id);
            throw;
        }
    }

    public async Task ArchiveAccountAsync(int id) {
        try {
            await using var conn = _db.GetConnection();
            await conn.ExecuteAsync(@"UPDATE Accounts SET IsArchived=1 WHERE Id=@id", new { id });
        }
        catch (Exception ex) {
            Log.Error(ex, "Error archiving account with ID {AccountId}[cite: 22].", id);
            throw;
        }
    }

    public async Task UnArchiveAccountAsync(int id) {
        try {
            await using var conn = _db.GetConnection();
            await conn.ExecuteAsync(@"UPDATE Accounts SET IsArchived=0 WHERE Id=@id", new { id });
        }
        catch (Exception ex) {
            Log.Error(ex, "Error unarchiving account with ID {AccountId}[cite: 22].", id);
            throw;
        }
    }

    public async Task DeleteAccountAsync(int id) {
        try {
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
        catch (Exception ex) {
            Log.Error(ex, "Error deleting account with ID {AccountId}[cite: 22].", id);
            throw;
        }
    }

    public async Task<bool> IsAccountInUseAsync(int accountId) {
        try {
            await using var conn = _db.GetConnection();

            var bills = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM Bills WHERE AccountId = @accountId OR ToAccountId = @accountId",
                new { accountId });
            if (bills > 0) return true;

            var buckets = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM Buckets WHERE AccountId = @accountId",
                new { accountId });
            if (buckets > 0) return true;

            var paychecks = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM Paychecks WHERE AccountId = @accountId",
                new { accountId });
            if (paychecks > 0) return true;

            var transactions = await conn.ExecuteScalarAsync<int>(
                $"SELECT COUNT(*) FROM Transactions WHERE AccountId = @accountId and not Description=@description",
                new { accountId, description = Constants.OpeningBalance });
            if (transactions > 0) return true;

            return false;
        }
        catch (Exception ex) {
            Log.Error(ex, "Error checking if account ID {AccountId} is in use[cite: 22].", accountId);
            return false;
        }
    }

    public async Task<(bool hasTransactions, decimal? openingBalance, DateTime? openingBalanceDate)>
        GetAccountBalanceOpeningStateAsync(int accountId) {
        try {
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
        catch (Exception ex) {
            Log.Error(ex, "Error getting account balance opening state for account ID {AccountId}[cite: 22].", accountId);
            return (false, null, null);
        }
    }

    public async Task<DateTime?> GetOldestOpeningBalanceAsync() {
        try {
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
        catch (Exception ex) {
            Log.Error(ex, "Error getting oldest opening balance[cite: 22].");
            return null;
        }
    }

    public async Task<DateTime?> GetOldestTransactionAsync() {
        try {
            await using var conn = _db.GetConnection();

            const string sql = @"
            SELECT MIN(TransactionDate) 
            FROM Transactions";

            var openingBalanceAsOf = await conn.ExecuteScalarAsync<DateTime?>(
                sql);

            return openingBalanceAsOf;
        }
        catch (Exception ex) {
            Log.Error(ex, "Error getting oldest transaction[cite: 22].");
            return null;
        }
    }
}