using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using StayOnTarget.Helpers;

using System.Windows.Markup;

namespace StayOnTarget.Controls
{
    public class NumericTextBox : TextBox
    {
        public NumericTextBox()
        {
            // Attach behavior
            TextBoxBehavior.SetIsNumericOnly(this, true);

            // Configure binding as soon as control loads
            Loaded += OnNumericTextBoxLoaded;
        }

private void OnNumericTextBoxLoaded(object sender, RoutedEventArgs e)
{
    BindingExpression bindingExpr = GetBindingExpression(TextProperty);

    if (bindingExpr != null && bindingExpr.ParentBinding != null)
    {
        Binding parentBinding = bindingExpr.ParentBinding;

        // Only clone/inject if Delay isn't already set
        if (parentBinding.Delay == 0)
        {
            // Respect their UpdateSourceTrigger if they explicitly set one; 
            // otherwise default to PropertyChanged
            UpdateSourceTrigger targetTrigger = parentBinding.UpdateSourceTrigger == UpdateSourceTrigger.Default
                ? UpdateSourceTrigger.PropertyChanged
                : parentBinding.UpdateSourceTrigger;

            // Only apply 500ms delay if we are using PropertyChanged
            int targetDelay = targetTrigger == UpdateSourceTrigger.PropertyChanged ? 750 : 0;

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
                UpdateSourceTrigger = targetTrigger,
                Delay = targetDelay
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
    }
}