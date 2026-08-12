using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using StayOnTarget.Helpers;

namespace StayOnTarget.Controls
{
    public class NumericTextBox : TextBox
    {
        public NumericTextBox()
        {
            // Apply standard accounting & numeric input alignments
            TextAlignment = TextAlignment.Right;
            VerticalContentAlignment = VerticalAlignment.Center;

            // Attach behavior
            TextBoxBehavior.SetIsNumericOnly(this, true);

            // Configure focus events for automatic text selection
            GotKeyboardFocus += OnGotKeyboardFocus;
            PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;

            // Configure binding as soon as control loads
            Loaded += OnNumericTextBoxLoaded;
        }

        #region Issue 1 Fix: Auto-Select Text on Focus

        private void OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            SelectAll();
        }

        private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // If the control was not focused before the click, set focus and select all text
            if (!IsKeyboardFocusWithin)
            {
                Focus();
                e.Handled = true;
            }
        }

        #endregion

        #region Issue 2 Fix: Decimal Point & Binding Strategy

        private void OnNumericTextBoxLoaded(object sender, RoutedEventArgs e)
        {
            BindingExpression bindingExpr = GetBindingExpression(TextProperty);

            if (bindingExpr != null && bindingExpr.ParentBinding != null)
            {
                Binding parentBinding = bindingExpr.ParentBinding;

                // Re-bind to update on LostFocus rather than PropertyChanged/Delay.
                // This prevents numeric converters/formatters from eating trailing decimal points mid-edit.
                if (parentBinding.UpdateSourceTrigger == UpdateSourceTrigger.Default || 
                    parentBinding.UpdateSourceTrigger == UpdateSourceTrigger.PropertyChanged)
                {
                    Binding newBinding = new Binding
                    {
                        Path = parentBinding.Path,
                        XPath = parentBinding.XPath,
                        Mode = parentBinding.Mode,
                        Converter = parentBinding.Converter,
                        ConverterParameter = parentBinding.ConverterParameter,
                        ConverterCulture = parentBinding.ConverterCulture,
                        StringFormat = parentBinding.StringFormat,
                        ValidatesOnDataErrors = parentBinding.ValidatesOnDataErrors,
                        ValidatesOnNotifyDataErrors = parentBinding.ValidatesOnNotifyDataErrors,
                        ValidatesOnExceptions = parentBinding.ValidatesOnExceptions,
                        UpdateSourceTrigger = UpdateSourceTrigger.LostFocus, // Solves trailing decimal issue
                        Delay = 0
                    };

                    if (parentBinding.Source != null)
                        newBinding.Source = parentBinding.Source;
                    else if (parentBinding.RelativeSource != null)
                        newBinding.RelativeSource = parentBinding.RelativeSource;
                    else if (!string.IsNullOrEmpty(parentBinding.ElementName))
                        newBinding.ElementName = parentBinding.ElementName;

                    SetBinding(TextProperty, newBinding);
                }
            }
        }

        #endregion
    }
}