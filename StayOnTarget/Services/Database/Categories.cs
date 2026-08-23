using Dapper;
using StayOnTarget.Models;
using Serilog;

namespace StayOnTarget.Services;

public partial class BudgetService
{
    // Category Operations
    public async Task<IEnumerable<Category>> GetAllCategoriesAsync(bool includeArchived = false)
    {
        try {
            await using var conn = _db.GetConnection();
            await conn.OpenAsync();

            return await conn.QueryAsync<Category>(
                "SELECT Categories.* FROM Categories WHERE Categories.IsArchived = 0 OR @includeArchived = 1 ORDER BY Categories.SortOrder, Categories.Name", 
                new { includeArchived = includeArchived ? 1 : 0 });
        }
        catch (Exception ex) {
            Log.Error(ex, "Error getting all categories[cite: 19].");
            return Enumerable.Empty<Category>();
        }
    }

    public async Task UpsertCategoryAsync(Category category)
    {
        try {
            await using var conn = _db.GetConnection();
            await conn.OpenAsync();

            if (category.Id == 0)
            {
                const string insertSql = @"
                INSERT INTO Categories (Name, HexColor, SortOrder, IsArchived) 
                VALUES (@Name, @HexColor, @SortOrder, @IsArchived);
                SELECT last_insert_rowid();";

                category.Id = await conn.ExecuteScalarAsync<int>(insertSql, category);
            }
            else
            {
                const string updateSql = @"
                UPDATE Categories 
                SET Name = @Name, HexColor = @HexColor, SortOrder = @SortOrder, IsArchived = @IsArchived 
                WHERE Id = @Id;";

                await conn.ExecuteAsync(updateSql, category);
            }
        }
        catch (Exception ex) {
            Log.Error(ex, "Error upserting category with ID {CategoryId}[cite: 19].", category.Id);
            throw;
        }
    }
    
    public async Task ArchiveCategoryAsync(int id)
    {
        try {
            await using var conn = _db.GetConnection();
            await conn.OpenAsync();
            await conn.ExecuteAsync(@"UPDATE Categories SET IsArchived=1 WHERE Id=@id", new { id });
        }
        catch (Exception ex) {
            Log.Error(ex, "Error archiving category with ID {CategoryId}[cite: 19].", id);
            throw;
        }
    }
    
    public async Task UnArchiveCategoryAsync(int id)
    {
        try {
            await using var conn = _db.GetConnection();
            await conn.OpenAsync();
            await conn.ExecuteAsync(@"UPDATE Categories SET IsArchived=0 WHERE Id=@id", new { id });
        }
        catch (Exception ex) {
            Log.Error(ex, "Error unarchiving category with ID {CategoryId}[cite: 19].", id);
            throw;
        }
    }
    
    public async Task DeleteCategoryAsync(int id)
    {
        try {
            await using var conn = _db.GetConnection();
            await conn.OpenAsync();
            await conn.ExecuteAsync("DELETE FROM Categories WHERE Id = @id", new { id });
        }
        catch (Exception ex) {
            Log.Error(ex, "Error deleting category with ID {CategoryId}[cite: 19].", id);
            throw;
        }
    }
    
    public async Task<bool> IsCategoryInUseAsync(int categoryId)
    {
        try {
            await using var conn = _db.GetConnection();
            await conn.OpenAsync();
            
            var count = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM SubCategories WHERE CategoryId = @categoryId", 
                new { categoryId });

            return count > 0;
        }
        catch (Exception ex) {
            Log.Error(ex, "Error checking if category with ID {CategoryId} is in use[cite: 19].", categoryId);
            return false;
        }
    }
}