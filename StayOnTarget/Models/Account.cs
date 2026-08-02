using System.Collections;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using StayOnTarget.ViewModels;

namespace StayOnTarget.Models;

public class Account : ViewModelBase, INotifyDataErrorInfo
{
    private string _name = string.Empty;
    private string _bankName = string.Empty;
    private decimal _balance;
    private DateTime _balanceAsOf = new DateTime(2026, 2, 19);
    private decimal _annualGrowthRate;
    private bool _includeInTotal = true;
    private AccountType _type = AccountType.Checking;
    private string _hexColor = "#FF0000FF"; // Default to Blue
    private MortgageDetails? _mortgageDetails;
    private CreditCardDetails? _creditCardDetails;
    private bool _isPrimary;
    private bool _isArchived;

    public int Id { get; set; }
    
    [Required(ErrorMessage = "Account name is required.")]
    [MinLength(1, ErrorMessage = "Account name cannot be empty.")]

    public string Name
    {
        get => _name;
        set {
            if (SetProperty(ref _name, value)) {
                ValidateProperty(nameof(Name), value);
            } 
        }
    }
    [Required(ErrorMessage = "Account bank name is required.")]
    [MinLength(1, ErrorMessage = "Account bank name cannot be empty.")]
    public string BankName {
        get => _bankName;
        set {
            if (SetProperty(ref _bankName, value)) {
                ValidateProperty(nameof(BankName), value);
            } 
        }
    }

    public decimal Balance
    {
        get => _balance;
        set => SetProperty(ref _balance, value);
    }

    public DateTime BalanceAsOf
    {
        get => _balanceAsOf;
        set => SetProperty(ref _balanceAsOf, value);
    }

    public decimal AnnualGrowthRate
    {
        get => _annualGrowthRate;
        set => SetProperty(ref _annualGrowthRate, value);
    }

    public bool IncludeInTotal
    {
        get => _includeInTotal;
        set => SetProperty(ref _includeInTotal, value);
    }

    public AccountType Type
    {
        get => _type;
        set => SetProperty(ref _type, value);
    }
    
    /// <summary>
    /// Indicates whether the account naturally carries a negative balance in net worth calculations.
    /// </summary>
    public bool IsLiability => Type switch
    {
        AccountType.CreditCard or 
            AccountType.Mortgage or 
            AccountType.PersonalLoan or 
            AccountType.StudentLoan or 
            AccountType.HELOC or 
            AccountType.Auto or 
            AccountType.OtherLiability => true,
        _ => false
    };

    /// <summary>
    /// Indicates whether this account requires loan/amortization/interest processing.
    /// </summary>
    public bool IsLoanAccount => Type switch
    {
        AccountType.Mortgage or 
            AccountType.StudentLoan or 
            AccountType.PersonalLoan or 
            AccountType.HELOC or 
            AccountType.Auto => true,
        _ => false
    };
    
    public bool UsesAmortizedLoanProjections => Type switch
    {
        AccountType.Mortgage or 
            AccountType.Auto or 
            AccountType.StudentLoan or 
            AccountType.PersonalLoan => true,

        // HELOC only amortizes if it is in repayment mode (or configured with a fixed payoff term)
        //AccountType.HELOC => IsHelocInRepaymentPhase, 

        _ => false
    };
    
    public string HexColor
    {
        get => _hexColor;
        set => SetProperty(ref _hexColor, value);
    }

    public MortgageDetails? MortgageDetails
    {
        get => _mortgageDetails;
        set => SetProperty(ref _mortgageDetails, value);
    }

    public CreditCardDetails? CreditCardDetails
    {
        get => _creditCardDetails;
        set => SetProperty(ref _creditCardDetails, value);
    }
    
    public bool IsPrimary
    {
        get => _isPrimary;
        set => SetProperty(ref _isPrimary, value);
    }
    
    public bool IsArchived
    {
        get => _isArchived;
        set => SetProperty(ref _isArchived, value);
    }
    
    public List<AccountAprHistory>? AccountAprHistory { get; set; }
    
    #region Error Validation
    
    // --- INotifyDataErrorInfo Implementation ---

    private readonly Dictionary<string, List<string>> _errors = new();
    public bool HasErrors => _errors.Any();
    public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

    public IEnumerable GetErrors(string propertyName)
    {
        return _errors.ContainsKey(propertyName) ? _errors[propertyName] : null;
    }

    private void ValidateProperty(string propertyName, object value)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(this) { MemberName = propertyName };

        Validator.TryValidateProperty(value, context, results);

        if (results.Any())
        {
            _errors[propertyName] = results.Select(r => r.ErrorMessage).ToList();
        }
        else
        {
            _errors.Remove(propertyName);
        }

        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        
        #endregion
    }
}