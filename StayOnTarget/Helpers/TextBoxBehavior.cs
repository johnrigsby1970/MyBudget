using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace StayOnTarget.Helpers;

public class TextBoxBehavior
{
    private static readonly Regex DecimalRegex = new(@"^-?\d*([.,]\d*)?$", RegexOptions.Compiled);

    public static readonly DependencyProperty IsNumericOnlyProperty =
        DependencyProperty.RegisterAttached(
            "IsNumericOnly",
            typeof(bool),
            typeof(TextBoxBehavior),
            new PropertyMetadata(false, OnIsNumericOnlyChanged));

    public static bool GetIsNumericOnly(DependencyObject obj) => (bool)obj.GetValue(IsNumericOnlyProperty);
    public static void SetIsNumericOnly(DependencyObject obj, bool value) => obj.SetValue(IsNumericOnlyProperty, value);

    private static void OnIsNumericOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextBox textBox)
        {
            textBox.PreviewTextInput -= NumberValidationTextBox;
            textBox.PreviewKeyDown -= TextBox_PreviewKeyDown_DisallowSpaceKey;
            DataObject.RemovePastingHandler(textBox, TextBoxPasting);

            if ((bool)e.NewValue)
            {
                textBox.PreviewTextInput += NumberValidationTextBox;
                textBox.PreviewKeyDown += TextBox_PreviewKeyDown_DisallowSpaceKey;
                DataObject.AddPastingHandler(textBox, TextBoxPasting);
            }
        }
    }

    private static void TextBox_PreviewKeyDown_DisallowSpaceKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            e.Handled = true;
        }
    }

    private static void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            // Treat comma as decimal point automatically if typed
            string textToInsert = e.Text == "," ? "." : e.Text;

            string currentText = textBox.Text;
            int selectionStart = textBox.SelectionStart;
            int selectionLength = textBox.SelectionLength;

            string proposedText = currentText.Remove(selectionStart, selectionLength)
                .Insert(selectionStart, textToInsert);

            if (!IsValidDecimal(proposedText))
            {
                e.Handled = true;
                return;
            }

            // If they typed a comma, replace it with a period in the text box
            if (e.Text == ",")
            {
                textBox.SelectedText = ".";
                textBox.SelectionStart = selectionStart + 1;
                textBox.SelectionLength = 0;
                e.Handled = true; // Handled manually
            }
        }
    }

    private static void TextBoxPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is TextBox textBox && e.DataObject.GetDataPresent(typeof(string)))
        {
            string rawPasteText = (string)e.DataObject.GetData(typeof(string));
        
            // Normalize commas to periods in pasted text
            string pasteText = rawPasteText.Replace(',', '.');

            string currentText = textBox.Text;
            int selectionStart = textBox.SelectionStart;
            int selectionLength = textBox.SelectionLength;

            string proposedText = currentText.Remove(selectionStart, selectionLength)
                .Insert(selectionStart, pasteText);

            if (!IsValidDecimal(proposedText))
            {
                e.CancelCommand(); // Block invalid text (e.g. multiple decimal points)
                return;
            }

            // If normalized text differs (comma was converted to period), perform manual insertion
            if (pasteText != rawPasteText)
            {
                e.CancelCommand(); // Cancel default paste
            
                // Insert normalized period-based string manually
                textBox.SelectedText = pasteText;
                textBox.SelectionStart = selectionStart + pasteText.Length;
                textBox.SelectionLength = 0;
            }
        }
        else
        {
            e.CancelCommand();
        }
    }

    private static bool IsValidDecimal(string text)
    {
        if (string.IsNullOrEmpty(text) || text == "-")
            return true; // Allow empty string or just a minus while typing

        return DecimalRegex.IsMatch(text);
    }
}