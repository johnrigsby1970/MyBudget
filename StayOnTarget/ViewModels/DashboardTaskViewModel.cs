namespace StayOnTarget.ViewModels;

public class DashboardTaskViewModel{
    public string? Title {get;set;}
    public decimal Amount {get;set;}
    public DateTime DueDate {get;set;}
    public StrategyTaskType TaskType {get;set;}
}