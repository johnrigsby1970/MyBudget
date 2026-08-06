namespace StayOnTarget.Data;

using Dapper;
using System.Data;

public static class CategorySeeder
{
    public static void SeedDefaultCategories(IDbConnection connection)
    {
        // Define the taxonomy: (Category, HexColor, Subcategories[])
        var defaultStructure = new[]
        {
            ("Housing", "#3B82F6", new[] { "Rent / Mortgage", "Property Taxes", "Homeowners Insurance", "Repairs & Maintenance" }),
            ("Utilities", "#06B6D4", new[] { "Electricity & Gas", "Water & Trash", "Internet", "Mobile Phone" }),
            ("Food & Dining", "#10B981", new[] { "Groceries", "Restaurants & Dining Out", "Coffee & Snacks" }),
            ("Transportation", "#F59E0B", new[] { "Auto Fuel", "Auto Maintenance", "Auto Insurance", "Parking & Tolls" }),
            ("Medical & Health", "#EF4444", new[] { "Health Insurance", "Doctor & Dental", "Prescriptions" }),
            ("Lifestyle & Personal", "#8B5CF6", new[] { "Clothing & Apparel", "Personal Care", "Subscriptions & Services" }),
            ("Entertainment & Fun", "#EC4899", new[] { "Movies & Streaming", "Hobbies & Gaming", "Concerts & Outings" }),
            ("Debt & Financial", "#64748B", new[] { "Credit Card Payments", "Student Loans", "Bank Fees" }),
            ("Savings & Investments", "#059669", new[] { "Emergency Fund", "Retirement", "Special Savings Goals" })
        };

        int categorySort = 1;

        foreach (var (categoryName, color, subcategories) in defaultStructure)
        {
            // 1. Check if Category already exists by name
            var categoryId = connection.QueryFirstOrDefault<long?>(
                "SELECT Id FROM Categories WHERE Name = @Name", 
                new { Name = categoryName });

            if (!categoryId.HasValue)
            {
                categoryId = connection.QuerySingle<long>(@"
                    INSERT INTO Categories (Name, HexColor, SortOrder, IsArchived)
                    VALUES (@Name, @HexColor, @SortOrder, 0);
                    SELECT last_insert_rowid();",
                    new 
                    { 
                        Name = categoryName, 
                        HexColor = color, 
                        SortOrder = categorySort 
                    });
            }

            int subcategorySort = 1;

            foreach (var subName in subcategories)
            {
                // 2. Check if SubCategory already exists under this Category
                var subExists = connection.QueryFirstOrDefault<int>(@"
                    SELECT COUNT(1) 
                    FROM Subcategories 
                    WHERE CategoryId = @CategoryId AND Name = @Name",
                    new { CategoryId = categoryId.Value, Name = subName });

                if (subExists == 0)
                {
                    // DefaultBucketId is explicitly set to NULL
                    connection.Execute(@"
                        INSERT INTO Subcategories (CategoryId, DefaultBucketId, Name, SortOrder, IsArchived)
                        VALUES (@CategoryId, NULL, @Name, @SortOrder, 0)",
                        new
                        {
                            CategoryId = categoryId.Value,
                            Name = subName,
                            SortOrder = subcategorySort
                        });
                }

                subcategorySort++;
            }

            categorySort++;
        }
    }
}