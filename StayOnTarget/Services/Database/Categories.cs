using Dapper;
using StayOnTarget.Models;

namespace StayOnTarget.Services;

public partial class BudgetService
{
    // Category Operations
    public async Task<IEnumerable<Category>> GetAllCategoriesAsync(bool includeArchived = false)
    {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();

        return await conn.QueryAsync<Category>(
            "SELECT Categories.* FROM Categories WHERE Categories.IsArchived = 0 OR @includeArchived = 1 ORDER BY Categories.SortOrder, Categories.Name", 
            new { includeArchived = includeArchived ? 1 : 0 });
    }

    public async Task UpsertCategoryAsync(Category category)
    {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();

        if (category.Id == 0)
        {
            const string insertSql = @"
            INSERT INTO Categories (Name, HexColor, SortOrder, IsArchived) 
            VALUES (@Name, @HexColor, @SortOrder, @IsArchived);
            SELECT last_insert_rowid();";

            // Dapper sets category.Id automatically if you capture the scalar return value
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
    
    public async Task ArchiveCategoryAsync(int id)
    {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();
        await conn.ExecuteAsync(@"UPDATE Categories SET IsArchived=1 WHERE Id=@id", new { id });
    }
    
    public async Task UnArchiveCategoryAsync(int id)
    {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();
        await conn.ExecuteAsync(@"UPDATE Categories SET IsArchived=0 WHERE Id=@id", new { id });
    }
    
    public async Task DeleteCategoryAsync(int id)
    {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();
        await conn.ExecuteAsync("DELETE FROM Categories WHERE Id = @id", new { id });
    }
    
    public async Task<bool> IsCategoryInUseAsync(int categoryId)
    {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();
        
        // Check Transactions table for SubCategoryId usage
        var count = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM SubCategories WHERE CategoryId = @categoryId", 
            new { categoryId });

        return count > 0;
    }
}