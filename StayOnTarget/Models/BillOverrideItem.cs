using CommunityToolkit.Mvvm.ComponentModel;

namespace StayOnTarget.Models;

public class OverrideItem : ObservableObject
{
    private string _monthKey = string.Empty;
    public string MonthKey
    {
        get => _monthKey;
        set => SetProperty(ref _monthKey, value);
    }

    private decimal _amount;
    public decimal Amount
    {
        get => _amount;
        set => SetProperty(ref _amount, value);
    }
}