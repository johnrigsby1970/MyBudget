using Dapper;
using StayOnTarget.Models;

namespace StayOnTarget.Services;

public partial class BudgetService
{
    // SubCategory Operations
    public async Task<IEnumerable<SubCategory>> GetAllSubCategoriesAsync(bool includeArchived = false)
    {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();

        return await conn.QueryAsync<SubCategory>(
            "SELECT SubCategories.*, Categories.Name As CategoryName, Buckets.Name As DefaultBucketName FROM SubCategories INNER JOIN Categories ON SubCategories.CategoryId = Categories.Id  LEFT OUTER JOIN Buckets ON SubCategories.DefaultBucketId = Buckets.Id WHERE SubCategories.IsArchived = 0 OR @includeArchived = 1 ORDER BY SubCategories.SortOrder, SubCategories.Name", 
            new { includeArchived = includeArchived ? 1 : 0 });
    }

    public async Task UpsertSubCategoryAsync(SubCategory subCategory)
    {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();

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
        await conn.OpenAsync();
        await conn.ExecuteAsync(@"UPDATE SubCategories SET IsArchived=1 WHERE Id=@id", new { id });
    }
    
    public async Task UnArchiveSubCategoryAsync(int id)
    {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();
        await conn.ExecuteAsync(@"UPDATE SubCategories SET IsArchived=0 WHERE Id=@id", new { id });
    }
    
    public async Task DeleteSubCategoryAsync(int id)
    {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();
        await conn.ExecuteAsync("DELETE FROM SubCategories WHERE Id = @id", new { id });
    }
    
    public async Task<bool> IsSubCategoryInUseAsync(int subCategoryId)
    {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();
        
        // Check Transactions table for SubCategoryId usage
        var count = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Transactions WHERE SubCategoryId = @subCategoryId", 
            new { subCategoryId });

        return count > 0;
    }
}