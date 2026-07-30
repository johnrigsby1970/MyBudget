using System.Windows;
using StayOnTarget.ViewModels;

namespace StayOnTarget;

public partial class ReassignAccountDependenciesDialog : Window
{
    public ReassignAccountDependenciesViewModel ViewModel => (ReassignAccountDependenciesViewModel)DataContext;

    public ReassignAccountDependenciesDialog(ReassignAccountDependenciesViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += ReassignAccountDependenciesDialog_Loaded;
    }

    private void ReassignAccountDependenciesDialog_Loaded(object sender, RoutedEventArgs e)
    {
        // Select the first visible tab
        foreach (var item in MainTabControl.Items)
        {
            if (item is FrameworkElement element && element.Visibility == Visibility.Visible)
            {
                MainTabControl.SelectedItem = item;
                break;
            }
        }
    }


    private void Save_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
