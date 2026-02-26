using MyBudget.Data;

namespace MyBudget.Services;

public partial class BudgetService
{
    private readonly DatabaseContext _db;

    public BudgetService()
    {
        _db = new DatabaseContext();
    }
}
