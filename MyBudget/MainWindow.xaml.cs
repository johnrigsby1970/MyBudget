using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MyBudget.ViewModels;
using System.Linq;
using MyBudget.Models;

namespace MyBudget;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window {
    private MainViewModel _mainViewModel;
    private AlternativeMainViewModel _alternativeMainViewModel;

    public MainWindow() {
        InitializeComponent();
        _mainViewModel = new MainViewModel();
        _alternativeMainViewModel = new AlternativeMainViewModel();
        DataContext = _mainViewModel;
        Loaded += MainWindow_Loaded;
    }

    private void UseNewLogicCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (UseNewLogicCheckBox.IsChecked == true)
        {
            DataContext = _alternativeMainViewModel;
            UpdateProjectionColumns(_alternativeMainViewModel.Accounts.Select(a => a.Name));
        }
        else
        {
            DataContext = _mainViewModel;
            UpdateProjectionColumns(_mainViewModel.Accounts.Select(a => a.Name));
        }
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _mainViewModel.PropertyChanged += Vm_PropertyChanged;
        _alternativeMainViewModel.PropertyChanged += Vm_PropertyChanged;
        
        if (DataContext is MainViewModel vm)
        {
            UpdateProjectionColumns(vm.Accounts.Select(a => a.Name));
        }
    }

    private void Vm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.Accounts))
        {
            if (sender == DataContext)
            {
                if (DataContext is MainViewModel vm) UpdateProjectionColumns(vm.Accounts.Select(a => a.Name));
                else if (DataContext is AlternativeMainViewModel avm) UpdateProjectionColumns(avm.Accounts.Select(a => a.Name));
            }
        }
    }

    private void UpdateProjectionColumns(IEnumerable<string> accountNames)
    {
        // Keep the first 5 columns (Date, Description, Amount, Total Balance, Period Net)
        while (ProjectionGrid.Columns.Count > 5)
        {
            ProjectionGrid.Columns.RemoveAt(5);
        }

        foreach (var accountName in accountNames)
        {
            var column = new DataGridTextColumn
            {
                Header = accountName,
                Binding = new Binding
                {
                    Converter = new AccountBalanceConverter(),
                    ConverterParameter = accountName,
                    StringFormat = "C"
                },
                Width = 90
            };
            ProjectionGrid.Columns.Add(column);
        }
    }
}

public class AccountBalanceConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is ProjectionItem item && parameter is string accountName)
        {
            return item.GetAccountBalance(accountName);
        }
        return 0m;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}