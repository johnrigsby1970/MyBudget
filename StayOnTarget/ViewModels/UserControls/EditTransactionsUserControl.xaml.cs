using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace StayOnTarget.ViewModels.UserControls;

public partial class EditTransactionsUserControl : UserControl
{
    public EditTransactionsUserControl()
    {
        InitializeComponent();
    }
    private void DescriptionComboBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if ((bool)e.NewValue)
        {
            FocusDescriptionInput();
        }
    }
    //
    private void AddAnotherButton_Click(object sender, RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            FocusDescriptionInput();
        }));
    }
    
    private void FocusDescriptionInput()
    {
        DescriptionComboBox?.FocusInput();
    }
}