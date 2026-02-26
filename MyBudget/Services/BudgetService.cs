using Dapper;
using MyBudget.Data;
using MyBudget.Models;
using System.Collections.Generic;
using System.Linq;

namespace MyBudget.Services;

public partial class BudgetService
{
    private readonly DatabaseContext _db;

    public BudgetService()
    {
        _db = new DatabaseContext();
    }
}
