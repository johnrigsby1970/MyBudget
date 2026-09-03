using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace StayOnTarget.ViewModels.UserControls;

public partial class EditAccountsUserControl : UserControl {
    public EditAccountsUserControl() {
        InitializeComponent();
    }
    private void DescriptionComboBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if ((bool)e.NewValue)
        {
            FocusNameInput();
        }
    }
    //
    private void AddAnotherButton_Click(object sender, RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            FocusNameInput();
        }));
    }
    
    private void FocusNameInput()
    {
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
        {
            if (NameTextBox == null) return;

            NameTextBox.Focus();
            Keyboard.Focus(NameTextBox);

            // Places caret cleanly at the end of the text
            NameTextBox.CaretIndex = NameTextBox.Text?.Length ?? 0;
            NameTextBox.SelectionLength = 0;
        }));
    }
}