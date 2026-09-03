
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace StayOnTarget.Helpers;

public static class BaseTextBoxBehavior
{
    private static bool _isMouseDown = false;

    public static readonly DependencyProperty SelectAllOnFocusProperty =
        DependencyProperty.RegisterAttached(
            "SelectAllOnFocus",
            typeof(bool),
            typeof(BaseTextBoxBehavior),
            new FrameworkPropertyMetadata(false, OnSelectAllOnFocusChanged));

    public static bool GetSelectAllOnFocus(DependencyObject obj) => (bool)obj.GetValue(SelectAllOnFocusProperty);
    public static void SetSelectAllOnFocus(DependencyObject obj, bool value) => obj.SetValue(SelectAllOnFocusProperty, value);

    private static void OnSelectAllOnFocusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextBox textBox)
        {
            textBox.GotKeyboardFocus -= TextBox_GotKeyboardFocus;
            textBox.SelectionChanged -= TextBox_SelectionChanged;
            textBox.PreviewMouseDown -= TextBox_PreviewMouseDown;
            textBox.MouseUp -= TextBox_MouseUp;
            textBox.PreviewKeyDown -= TextBox_PreviewKeyDown;

            if ((bool)e.NewValue)
            {
                textBox.GotKeyboardFocus += TextBox_GotKeyboardFocus;
                textBox.SelectionChanged += TextBox_SelectionChanged;
                textBox.PreviewMouseDown += TextBox_PreviewMouseDown;
                textBox.MouseUp += TextBox_MouseUp;
                textBox.PreviewKeyDown += TextBox_PreviewKeyDown;
            }
        }
    }

    private static void TextBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        _isMouseDown = true;
    }

    private static void TextBox_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _isMouseDown = false;
    }

    private static void TextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox textBox && !_isMouseDown)
        {
            textBox.CaretIndex = textBox.Text.Length;
            textBox.SelectionLength = 0;
        }
    }

    private static void TextBox_SelectionChanged(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && textBox.IsKeyboardFocusWithin && !_isMouseDown)
        {
            // Only clear full-text auto-selection forced by WPF focus routines
            if (textBox.SelectionLength > 0 && textBox.SelectionLength == textBox.Text.Length)
            {
                textBox.SelectionLength = 0;
                textBox.CaretIndex = textBox.Text.Length;
            }
        }
    }

    private static void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox textBox && textBox.IsKeyboardFocusWithin)
        {
            if (e.Key == Key.Back || e.Key == Key.Delete)
            {
                int currentCaret = textBox.CaretIndex;

                // Use ContextIdle so this executes AFTER WPF's ComboBox internal selection-sync 
                // and collection refresh logic completely finishes.
                textBox.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
                {
                    if (textBox.IsKeyboardFocusWithin)
                    {
                        // Calculate expected caret index
                        int expectedCaret = e.Key == Key.Back ? Math.Max(0, currentCaret - 1) : currentCaret;

                        // Ensure position is within bounds and restore caret
                        if (expectedCaret <= textBox.Text.Length)
                        {
                            textBox.CaretIndex = expectedCaret;
                            textBox.SelectionLength = 0;
                        }
                    }
                }));
            }
        }
    }
}