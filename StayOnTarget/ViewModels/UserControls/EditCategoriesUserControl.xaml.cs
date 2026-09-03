using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace StayOnTarget.ViewModels.UserControls;

public partial class EditCategoriesUserControl : UserControl 
{
    public EditCategoriesUserControl() 
    {
        InitializeComponent();
        IsVisibleChanged += EditCategoriesUserControl_IsVisibleChanged;
    }

    private void EditCategoriesUserControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if ((bool)e.NewValue)
        {
            FocusNameInput();
        }
    }

    private void FocusNameInput()
    {
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
        {
            if (NameTextBox == null) return;

            NameTextBox.Focus();
            Keyboard.Focus(NameTextBox);

            NameTextBox.CaretIndex = NameTextBox.Text?.Length ?? 0;
            NameTextBox.SelectionLength = 0;
        }));
    }
}