using Dapper;
using StayOnTarget.Models;
using Serilog;

namespace StayOnTarget.Services;

public partial class BudgetService
{
    // SubCategory Operations
    public async Task<IEnumerable<SubCategory>> GetAllSubCategoriesAsync(bool includeArchived = false)
    {
        try {
            await using var conn = _db.GetConnection();
            await conn.OpenAsync();

            return await conn.QueryAsync<SubCategory>(
                "SELECT SubCategories.*, Categories.Name As CategoryName, Buckets.Name As DefaultBucketName FROM SubCategories INNER JOIN Categories ON SubCategories.CategoryId = Categories.Id  LEFT OUTER JOIN Buckets ON SubCategories.DefaultBucketId = Buckets.Id WHERE SubCategories.IsArchived = 0 OR @includeArchived = 1 ORDER BY SubCategories.SortOrder, SubCategories.Name", 
                new { includeArchived = includeArchived ? 1 : 0 });
        }
        catch (Exception ex) {
            Log.Error(ex, "Error getting all subcategories.");
            return Enumerable.Empty<SubCategory>();
        }
    }

    public async Task UpsertSubCategoryAsync(SubCategory subCategory)
    {
        try {
            await using var conn = _db.GetConnection();
            await conn.OpenAsync();

            if (subCategory.Id == 0)
            {
                await conn.ExecuteAsync(@"INSERT INTO SubCategories (CategoryId, DefaultBucketId, Name, SortOrder, IsArchived) 
                               VAlUES (@CategoryId, @DefaultBucketId, @Name, @SortOrder, @IsArchived)", subCategory);
            }
            else
            {
                await conn.ExecuteAsync(@"UPDATE SubCategories SET CategoryId=@CategoryId, DefaultBucketId=@DefaultBucketId, Name=@Name, SortOrder=@SortOrder, IsArchived=@IsArchived WHERE Id=@Id", subCategory);
            }
        }
        catch (Exception ex) {
            Log.Error(ex, "Error upserting subcategory with ID {SubCategoryId}.", subCategory.Id);
            throw;
        }
    }
    
    public async Task ArchiveSubCategoryAsync(int id)
    {
        try {
            await using var conn = _db.GetConnection();
            await conn.OpenAsync();
            await conn.ExecuteAsync(@"UPDATE SubCategories SET IsArchived=1 WHERE Id=@id", new { id });
        }
        catch (Exception ex) {
            Log.Error(ex, "Error archiving subcategory with ID {SubCategoryId}.", id);
            throw;
        }
    }
    
    public async Task UnArchiveSubCategoryAsync(int id)
    {
        try {
            await using var conn = _db.GetConnection();
            await conn.OpenAsync();
            await conn.ExecuteAsync(@"UPDATE SubCategories SET IsArchived=0 WHERE Id=@id", new { id });
        }
        catch (Exception ex) {
            Log.Error(ex, "Error unarchiving subcategory with ID {SubCategoryId}.", id);
            throw;
        }
    }
    
    public async Task DeleteSubCategoryAsync(int id)
    {
        try {
            await using var conn = _db.GetConnection();
            await conn.OpenAsync();
            await conn.ExecuteAsync("DELETE FROM SubCategories WHERE Id = @id", new { id });
        }
        catch (Exception ex) {
            Log.Error(ex, "Error deleting subcategory with ID {SubCategoryId}.", id);
            throw;
        }
    }
    
    public async Task<bool> IsSubCategoryInUseAsync(int subCategoryId)
    {
        try {
            await using var conn = _db.GetConnection();
            await conn.OpenAsync();
            
            var count = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM Transactions WHERE SubCategoryId = @subCategoryId", 
                new { subCategoryId });

            return count > 0;
        }
        catch (Exception ex) {
            Log.Error(ex, "Error checking if subcategory with ID {SubCategoryId} is in use.", subCategoryId);
            return false;
        }
    }
}