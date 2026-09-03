using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace StayOnTarget.Helpers;

public static class ComboBoxBehavior
{
    public static readonly DependencyProperty DisableAutoSelectOnFocusProperty =
        DependencyProperty.RegisterAttached(
            "DisableAutoSelectOnFocus",
            typeof(bool),
            typeof(ComboBoxBehavior),
            new PropertyMetadata(false, OnDisableAutoSelectOnFocusChanged));

    public static bool GetDisableAutoSelectOnFocus(DependencyObject obj) => (bool)obj.GetValue(DisableAutoSelectOnFocusProperty);
    public static void SetDisableAutoSelectOnFocus(DependencyObject obj, bool value) => obj.SetValue(DisableAutoSelectOnFocusProperty, value);

    private static void OnDisableAutoSelectOnFocusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ComboBox comboBox && (bool)e.NewValue)
        {
            comboBox.GotFocus += (s, args) =>
            {
                comboBox.Dispatcher.BeginInvoke(new Action(() =>
                {
                    var textBox = comboBox.Template.FindName("PART_EditableTextBox", comboBox) as TextBox;
                    if (textBox != null)
                    {
                        textBox.SelectionLength = 0;
                        textBox.CaretIndex = textBox.Text.Length;
                    }
                }), DispatcherPriority.Background);
            };
        }
    }
}