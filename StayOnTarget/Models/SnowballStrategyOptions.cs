using CommunityToolkit.Mvvm.ComponentModel;

namespace StayOnTarget.Models;

public enum SurplusAllocationTarget { PayDownDebt, InvestSurplus, Hybrid }
public enum SnowballSortStrategy { LowestBalanceFirst, HighestInterestFirst }
public enum InvestmentStrategy { MaximizeYield, MinimizeLoss, PrioritizeRothLimits }

public class SnowballStrategyOptions : ObservableObject
{
    private bool _enableSnowball;
    public bool EnableSnowball
    {
        get => _enableSnowball;
        set => SetProperty(ref _enableSnowball, value);
    }
    
    // Target Selection: Debt vs. Wealth Building
    private SurplusAllocationTarget _primaryTarget = SurplusAllocationTarget.PayDownDebt;
    public SurplusAllocationTarget PrimaryTarget
    {
        get => _primaryTarget;
        set => SetProperty(ref _primaryTarget, value);
    }
    
    // Strategy Ordering
    private SnowballSortStrategy _debtSortStrategy = SnowballSortStrategy.LowestBalanceFirst;
    public SnowballSortStrategy DebtSortStrategy
    {
        get => _debtSortStrategy;
        set => SetProperty(ref _debtSortStrategy, value);
    }

    private InvestmentStrategy _investmentStrategy = InvestmentStrategy.MaximizeYield;
    public InvestmentStrategy InvestmentStrategy
    {
        get => _investmentStrategy;
        set => SetProperty(ref _investmentStrategy, value);
    }
    
    // Allocation Settings
    private decimal _surplusSweepPercentage = 0.50m;
    public decimal SurplusSweepPercentage
    {
        get => _surplusSweepPercentage;
        set => SetProperty(ref _surplusSweepPercentage, value);
    }

    private decimal _checkingSafetyThresholdPct = 0.50m;
    public decimal CheckingSafetyThresholdPct
    {
        get => _checkingSafetyThresholdPct;
        set => SetProperty(ref _checkingSafetyThresholdPct, value);
    }
    
    // Account Limits
    private decimal _annualRothContributionLimit = 7000m;
    public decimal AnnualRothContributionLimit
    {
        get => _annualRothContributionLimit;
        set => SetProperty(ref _annualRothContributionLimit, value);
    }

    private Dictionary<int, decimal> _currentYearRothContributions = new();
    public Dictionary<int, decimal> CurrentYearRothContributions
    {
        get => _currentYearRothContributions;
        set => SetProperty(ref _currentYearRothContributions, value);
    }
}
