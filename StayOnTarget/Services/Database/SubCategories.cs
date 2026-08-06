using Dapper;
using StayOnTarget.Models;

namespace StayOnTarget.Services;

public partial class BudgetService
{
    // SubCategory Operations
    public async Task<IEnumerable<SubCategory>> GetAllSubCategoriesAsync(bool includeArchived = false)
    {
        await using var conn = _db.GetConnection();
        return await conn.QueryAsync<SubCategory>("SELECT * FROM SubCategories WHERE (IsArchived=0 OR IsArchived = @includeArchived)", new { includeArchived=(includeArchived ? 1: 0) });
    }

    public async Task UpsertSubCategoryAsync(SubCategory subCategory)
    {
        await using var conn = _db.GetConnection();
        if (subCategory.Id == 0)
        {
            await conn.ExecuteAsync(@"INSERT INTO SubCategories (CategoryId, DefaultBucketId, Name, SortOrder, IsArchived) 
                           VALUES (@CategoryId, @DefaultBucketId, @Name, @SortOrder, @IsArchived)", subCategory);
        }
        else
        {
            await conn.ExecuteAsync(@"UPDATE SubCategories SET CategoryId=@CategoryId, DefaultBucketId=@DefaultBucketId, Name=@Name, SortOrder=@SortOrder, IsArchived=@IsArchived WHERE Id=@Id", subCategory);
        }
    }
    
    public async Task ArchiveSubCategoryAsync(int id)
    {
        await using var conn = _db.GetConnection();
        await conn.ExecuteAsync(@"UPDATE SubCategories SET IsArchived=1 WHERE Id=@id", new { id });
    }
    
    public async Task UnArchiveSubCategoryAsync(int id)
    {
        await using var conn = _db.GetConnection();
        await conn.ExecuteAsync(@"UPDATE SubCategories SET IsArchived=0 WHERE Id=@id", new { id });
    }
    
    public async Task DeleteSubCategoryAsync(int id)
    {
        // if (await IsSubCategoryInUseAsync(id)) {
        //     await using var conn = _db.GetConnection();
        //     await conn.ExecuteAsync("UPDATE SubCategories SET IsArchived = 1 WHERE Id = @id", new { id });
        // }
        // else {
            await using var conn = _db.GetConnection();
            await conn.ExecuteAsync("DELETE FROM SubCategories WHERE Id = @id", new { id });
        //}
    }
    
    public async Task<bool> IsSubCategoryInUseAsync(int subCategoryId)
    {
        await using var conn = _db.GetConnection();
        
        // Check Transactions (AccountId or ToAccountId)
        var transactions = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Transactions WHERE SubCategories = @subCategoryId", 
            new { subCategoryId });
        if (transactions > 0) return true;
        
        return false;
    }
}