using System.Collections;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace StayOnTarget.Controls;

public partial class AutoCompleteTextBox : UserControl
{
    private readonly TextBox _inputTextBox;
    private readonly Popup _suggestionPopup;
    private readonly ListBox _suggestionListBox;

    private string _textBeforeArrowNavigation = string.Empty;
    private bool _isNavigatingWithKeys = false;
    private bool _isInternalTextChange = false;

    public AutoCompleteTextBox()
    {
        _inputTextBox = new TextBox();
        _inputTextBox.PreviewKeyDown += InputTextBox_PreviewKeyDown;
        _inputTextBox.LostFocus += InputTextBox_LostFocus;

        var textBinding = new Binding(nameof(Text))
        {
            Source = this,
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
            Delay = 150
        };
        _inputTextBox.SetBinding(TextBox.TextProperty, textBinding);

        _suggestionListBox = new ListBox
        {
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(_suggestionListBox, ScrollBarVisibility.Disabled);

        // Fix 2: Use SelectionChanged to capture item pick without click-through
        _suggestionListBox.SelectionChanged += SuggestionListBox_SelectionChanged;

        var border = new Border
        {
            Background = SystemColors.WindowBrush,
            BorderBrush = SystemColors.ControlDarkBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            MaxHeight = 200,
            Margin = new Thickness(0, 2, 0, 0),
            Child = _suggestionListBox
        };

        var widthBinding = new Binding(nameof(FrameworkElement.ActualWidth))
        {
            Source = _inputTextBox
        };
        border.SetBinding(FrameworkElement.MinWidthProperty, widthBinding);

        _suggestionPopup = new Popup
        {
            PlacementTarget = _inputTextBox,
            Placement = PlacementMode.Bottom,
            IsOpen = false,
            StaysOpen = false,
            AllowsTransparency = true,
            Focusable = false,
            Child = border
        };

        var grid = new Grid();
        grid.Children.Add(_inputTextBox);
        grid.Children.Add(_suggestionPopup);

        Content = grid;
    }

    #region Dependency Properties

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(AutoCompleteTextBox),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextChanged));

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(AutoCompleteTextBox),
            new PropertyMetadata(null, OnItemsSourceChanged));

    public static readonly DependencyProperty DisplayMemberPathProperty =
        DependencyProperty.Register(nameof(DisplayMemberPath), typeof(string), typeof(AutoCompleteTextBox),
            new PropertyMetadata(string.Empty, OnDisplayMemberPathChanged));

    public static readonly DependencyProperty SelectedValuePathProperty =
        DependencyProperty.Register(nameof(SelectedValuePath), typeof(string), typeof(AutoCompleteTextBox),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SelectedValueProperty =
        DependencyProperty.Register(nameof(SelectedValue), typeof(object), typeof(AutoCompleteTextBox),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public IEnumerable ItemsSource
    {
        get => (IEnumerable)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public string DisplayMemberPath
    {
        get => (string)GetValue(DisplayMemberPathProperty);
        set => SetValue(DisplayMemberPathProperty, value);
    }

    public string SelectedValuePath
    {
        get => (string)GetValue(SelectedValuePathProperty);
        set => SetValue(SelectedValuePathProperty, value);
    }

    public object? SelectedValue
    {
        get => GetValue(SelectedValueProperty);
        set => SetValue(SelectedValueProperty, value);
    }

    public ICollectionView? FilteredView { get; private set; }

    #endregion

    private static void OnDisplayMemberPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (AutoCompleteTextBox)d;
        control._suggestionListBox.DisplayMemberPath = (string)e.NewValue;
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (AutoCompleteTextBox)d;
        control.SetupCollectionView();
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (AutoCompleteTextBox)d;
        if (!control._isInternalTextChange)
        {
            control.FilterItems();
        }
    }

    // private void SetupCollectionView()
    // {
    //     if (ItemsSource == null)
    //     {
    //         FilteredView = null;
    //         _suggestionListBox.ItemsSource = null;
    //         return;
    //     }
    //
    //     FilteredView = CollectionViewSource.GetDefaultView(ItemsSource);
    //     _suggestionListBox.ItemsSource = FilteredView;
    //     FilterItems();
    // }

    private void SetupCollectionView()
    {
        if (ItemsSource == null)
        {
            FilteredView = null;
            _suggestionListBox.ItemsSource = null;
            return;
        }

        // Wrap in a ListCast / IList check to build an independent CollectionView instance
        if (ItemsSource is IList list)
        {
            FilteredView = new ListCollectionView(list);
        }
        else
        {
            // Fallback for non-IList IEnumerable sources
            FilteredView = new ListCollectionView(new ArrayList(ItemsSource is ICollection col ? col : System.Linq.Enumerable.ToList(ItemsSource.Cast<object>())));
        }

        _suggestionListBox.ItemsSource = FilteredView;
        FilterItems();
    }
    
    private void FilterItems()
    {
        if (FilteredView == null) return;

        var searchText = Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(searchText))
        {
            FilteredView.Filter = _ => false;
            _suggestionPopup.IsOpen = false;
            return;
        }

        FilteredView.Filter = item =>
        {
            if (item == null) return false;
            var displayVal = GetPropertyValue(item, DisplayMemberPath);
            return displayVal != null && displayVal.StartsWith(searchText, StringComparison.OrdinalIgnoreCase);
        };

        bool hasMatches = !FilteredView.IsEmpty;
        _suggestionPopup.IsOpen = hasMatches;

        if (hasMatches && !_isNavigatingWithKeys)
        {
            _suggestionListBox.SelectedIndex = -1;
        }
    }

    // Fix 3: Up/Down Arrow key support with live description updating
    // Change signature from InputTextBox_KeyDown to InputTextBox_PreviewKeyDown
private void InputTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
{
    if (!_suggestionPopup.IsOpen || FilteredView == null || FilteredView.IsEmpty)
    {
        return;
    }

    switch (e.Key)
    {
        case Key.Down:
            if (!_isNavigatingWithKeys)
            {
                _textBeforeArrowNavigation = Text;
                _isNavigatingWithKeys = true;
            }

            int nextIndex = _suggestionListBox.SelectedIndex + 1;
            if (nextIndex < _suggestionListBox.Items.Count)
            {
                _suggestionListBox.SelectedIndex = nextIndex;
                _suggestionListBox.ScrollIntoView(_suggestionListBox.SelectedItem);
                PreviewSelectedItem(_suggestionListBox.SelectedItem);
            }
            e.Handled = true; // Prevents focus moving to control below
            break;

        case Key.Up:
            if (!_isNavigatingWithKeys)
            {
                _textBeforeArrowNavigation = Text;
                _isNavigatingWithKeys = true;
            }

            int prevIndex = _suggestionListBox.SelectedIndex - 1;
            if (prevIndex >= 0)
            {
                _suggestionListBox.SelectedIndex = prevIndex;
                _suggestionListBox.ScrollIntoView(_suggestionListBox.SelectedItem);
                PreviewSelectedItem(_suggestionListBox.SelectedItem);
            }
            else if (prevIndex == -1)
            {
                _suggestionListBox.SelectedIndex = -1;
                RestoreTextBeforeNavigation();
            }
            e.Handled = true; // Prevents focus moving to control above
            break;

        case Key.Enter:
        case Key.Tab:
            if (_suggestionListBox.SelectedItem != null)
            {
                CommitSelection(_suggestionListBox.SelectedItem);
                e.Handled = true; // Prevents Tab/Enter from submitting form prematurely
            }
            break;

        case Key.Escape:
            RestoreTextBeforeNavigation();
            _suggestionPopup.IsOpen = false;
            e.Handled = true;
            break;
    }
}

    private void PreviewSelectedItem(object item)
    {
        if (item == null) return;

        _isInternalTextChange = true;
        try
        {
            var displayValue = GetPropertyValue(item, DisplayMemberPath);
            var idValue = GetPropertyValue(item, SelectedValuePath);

            Text = displayValue ?? string.Empty;
            SelectedValue = idValue;

            _inputTextBox.CaretIndex = _inputTextBox.Text.Length;
        }
        finally
        {
            _isInternalTextChange = false;
        }
    }

    private void RestoreTextBeforeNavigation()
    {
        _isInternalTextChange = true;
        try
        {
            Text = _textBeforeArrowNavigation;
            _inputTextBox.CaretIndex = _inputTextBox.Text.Length;
            _isNavigatingWithKeys = false;
            ResolveSelectedValueOnLostFocus();
        }
        finally
        {
            _isInternalTextChange = false;
        }
    }

    // Fix 2: SelectionChanged handles mouse clicks without passing clicks through
    private void SuggestionListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suggestionListBox.SelectedItem != null && !_isNavigatingWithKeys)
        {
            CommitSelection(_suggestionListBox.SelectedItem);
        }
    }

    private void CommitSelection(object item)
    {
        _isInternalTextChange = true;
        try
        {
            var displayValue = GetPropertyValue(item, DisplayMemberPath);
            var idValue = GetPropertyValue(item, SelectedValuePath);

            Text = displayValue ?? string.Empty;
            SelectedValue = idValue;

            _inputTextBox.CaretIndex = _inputTextBox.Text.Length;
            _isNavigatingWithKeys = false;
            _suggestionPopup.IsOpen = false;
        }
        finally
        {
            _isInternalTextChange = false;
        }
    }

    private void InputTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_suggestionListBox.IsKeyboardFocusWithin) return;

        ResolveSelectedValueOnLostFocus();
        _isNavigatingWithKeys = false;
        _suggestionPopup.IsOpen = false;
    }

    // Fix 1: Correct description casing when exact case-insensitive match is found
    private void ResolveSelectedValueOnLostFocus()
    {
        if (ItemsSource == null) return;

        var searchText = Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(searchText))
        {
            SelectedValue = null;
            return;
        }

        foreach (var item in ItemsSource)
        {
            var displayVal = GetPropertyValue(item, DisplayMemberPath);
            if (string.Equals(displayVal, searchText, StringComparison.OrdinalIgnoreCase))
            {
                // Correct casing to match suggestion directly
                if (displayVal != null && Text != displayVal)
                {
                    _isInternalTextChange = true;
                    try
                    {
                        Text = displayVal;
                    }
                    finally
                    {
                        _isInternalTextChange = false;
                    }
                }

                SelectedValue = GetPropertyValue(item, SelectedValuePath);
                return;
            }
        }

        SelectedValue = null;
    }

    private string? GetPropertyValue(object item, string propertyName)
    {
        if (item == null || string.IsNullOrEmpty(propertyName)) return item?.ToString();
        var prop = item.GetType().GetProperty(propertyName);
        return prop?.GetValue(item)?.ToString();
    }
    
    
    public void FocusInput()
    {
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
        {
            _inputTextBox.Focus();
            Keyboard.Focus(_inputTextBox);
            _inputTextBox.CaretIndex = _inputTextBox.Text.Length;
            _inputTextBox.SelectionLength = 0;
        }));
    }
}

// using System;
// using System.Collections;
// using System.ComponentModel;
// using System.Windows;
// using System.Windows.Controls;
// using System.Windows.Controls.Primitives;
// using System.Windows.Data;
// using System.Windows.Input;
// using System.Windows.Media;
//
// namespace StayOnTarget.Controls;
//
// public partial class AutoCompleteTextBox : UserControl
// {
//     private readonly TextBox _inputTextBox;
//     private readonly Popup _suggestionPopup;
//     private readonly ListBox _suggestionListBox;
//
//     public AutoCompleteTextBox()
//     {
//         _inputTextBox = new TextBox();
//         _inputTextBox.KeyDown += InputTextBox_KeyDown;
//         _inputTextBox.LostFocus += InputTextBox_LostFocus;
//
//         var textBinding = new Binding(nameof(Text))
//         {
//             Source = this,
//             Mode = BindingMode.TwoWay,
//             UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
//             Delay = 150
//         };
//         _inputTextBox.SetBinding(TextBox.TextProperty, textBinding);
//
//         _suggestionListBox = new ListBox
//         {
//             BorderThickness = new Thickness(0),
//             Background = Brushes.Transparent,
//         };
//         
//         // Set via attached property helper
//         ScrollViewer.SetHorizontalScrollBarVisibility(_suggestionListBox, ScrollBarVisibility.Disabled);
//         
//         _suggestionListBox.PreviewMouseLeftButtonDown += SuggestionListBox_PreviewMouseLeftButtonDown;
//
//         var border = new Border
//         {
//             Background = SystemColors.WindowBrush,
//             BorderBrush = SystemColors.ControlDarkBrush,
//             BorderThickness = new Thickness(1),
//             CornerRadius = new CornerRadius(4),
//             MaxHeight = 200,
//             Margin = new Thickness(0, 2, 0, 0),
//             Child = _suggestionListBox
//         };
//
//         var widthBinding = new Binding(nameof(FrameworkElement.ActualWidth))
//         {
//             Source = _inputTextBox
//         };
//         border.SetBinding(FrameworkElement.MinWidthProperty, widthBinding);
//
//         _suggestionPopup = new Popup
//         {
//             PlacementTarget = _inputTextBox,
//             Placement = PlacementMode.Bottom,
//             IsOpen = false,
//             StaysOpen = false,
//             AllowsTransparency = true,
//             Focusable = false,
//             Child = border
//         };
//
//         var grid = new Grid();
//         grid.Children.Add(_inputTextBox);
//         grid.Children.Add(_suggestionPopup);
//
//         Content = grid;
//     }
//
//     #region Dependency Properties
//
//     public static readonly DependencyProperty TextProperty =
//         DependencyProperty.Register(nameof(Text), typeof(string), typeof(AutoCompleteTextBox),
//             new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextChanged));
//
//     public static readonly DependencyProperty ItemsSourceProperty =
//         DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(AutoCompleteTextBox),
//             new PropertyMetadata(null, OnItemsSourceChanged));
//
//     public static readonly DependencyProperty DisplayMemberPathProperty =
//         DependencyProperty.Register(nameof(DisplayMemberPath), typeof(string), typeof(AutoCompleteTextBox),
//             new PropertyMetadata(string.Empty, OnDisplayMemberPathChanged));
//
//     public static readonly DependencyProperty SelectedValuePathProperty =
//         DependencyProperty.Register(nameof(SelectedValuePath), typeof(string), typeof(AutoCompleteTextBox),
//             new PropertyMetadata(string.Empty));
//
//     public static readonly DependencyProperty SelectedValueProperty =
//         DependencyProperty.Register(nameof(SelectedValue), typeof(object), typeof(AutoCompleteTextBox),
//             new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
//
//     public string Text
//     {
//         get => (string)GetValue(TextProperty);
//         set => SetValue(TextProperty, value);
//     }
//
//     public IEnumerable ItemsSource
//     {
//         get => (IEnumerable)GetValue(ItemsSourceProperty);
//         set => SetValue(ItemsSourceProperty, value);
//     }
//
//     public string DisplayMemberPath
//     {
//         get => (string)GetValue(DisplayMemberPathProperty);
//         set => SetValue(DisplayMemberPathProperty, value);
//     }
//
//     public string SelectedValuePath
//     {
//         get => (string)GetValue(SelectedValuePathProperty);
//         set => SetValue(SelectedValuePathProperty, value);
//     }
//
//     public object? SelectedValue
//     {
//         get => GetValue(SelectedValueProperty);
//         set => SetValue(SelectedValueProperty, value);
//     }
//
//     // Public collection view for filtering matching items
//     public ICollectionView? FilteredView { get; private set; }
//
//     #endregion
//
//     private static void OnDisplayMemberPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
//     {
//         var control = (AutoCompleteTextBox)d;
//         control._suggestionListBox.DisplayMemberPath = (string)e.NewValue;
//     }
//
//     private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
//     {
//         var control = (AutoCompleteTextBox)d;
//         control.SetupCollectionView();
//     }
//
//     private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
//     {
//         var control = (AutoCompleteTextBox)d;
//         control.FilterItems();
//     }
//
//     private void SetupCollectionView()
//     {
//         if (ItemsSource == null)
//         {
//             FilteredView = null;
//             _suggestionListBox.ItemsSource = null;
//             return;
//         }
//
//         FilteredView = CollectionViewSource.GetDefaultView(ItemsSource);
//         _suggestionListBox.ItemsSource = FilteredView;
//         FilterItems();
//     }
//
//     private void FilterItems()
//     {
//         if (FilteredView == null) return;
//
//         var searchText = Text ?? string.Empty;
//
//         if (string.IsNullOrWhiteSpace(searchText))
//         {
//             FilteredView.Filter = _ => false;
//             _suggestionPopup.IsOpen = false;
//             return;
//         }
//
//         FilteredView.Filter = item =>
//         {
//             if (item == null) return false;
//             var displayVal = GetPropertyValue(item, DisplayMemberPath);
//             return displayVal != null && displayVal.StartsWith(searchText, StringComparison.OrdinalIgnoreCase);
//         };
//
//         bool hasMatches = !FilteredView.IsEmpty;
//         _suggestionPopup.IsOpen = hasMatches;
//
//         if (hasMatches)
//         {
//             _suggestionListBox.SelectedIndex = -1;
//         }
//     }
//
//     private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
//     {
//         if (!_suggestionPopup.IsOpen || FilteredView == null || FilteredView.IsEmpty)
//         {
//             if (e.Key == Key.Down && _suggestionPopup.IsOpen)
//             {
//                 _suggestionListBox.SelectedIndex = 0;
//                 e.Handled = true;
//             }
//             return;
//         }
//
//         switch (e.Key)
//         {
//             case Key.Down:
//                 int nextIndex = _suggestionListBox.SelectedIndex + 1;
//                 if (nextIndex < _suggestionListBox.Items.Count)
//                 {
//                     _suggestionListBox.SelectedIndex = nextIndex;
//                     _suggestionListBox.ScrollIntoView(_suggestionListBox.SelectedItem);
//                 }
//                 e.Handled = true;
//                 break;
//
//             case Key.Up:
//                 int prevIndex = _suggestionListBox.SelectedIndex - 1;
//                 if (prevIndex >= 0)
//                 {
//                     _suggestionListBox.SelectedIndex = prevIndex;
//                     _suggestionListBox.ScrollIntoView(_suggestionListBox.SelectedItem);
//                 }
//                 e.Handled = true;
//                 break;
//
//             case Key.Enter:
//             case Key.Tab:
//                 if (_suggestionListBox.SelectedItem != null)
//                 {
//                     CommitSelection(_suggestionListBox.SelectedItem);
//                     e.Handled = true;
//                 }
//                 break;
//
//             case Key.Escape:
//                 _suggestionPopup.IsOpen = false;
//                 e.Handled = true;
//                 break;
//         }
//     }
//
//     private void SuggestionListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
//     {
//         var item = (e.OriginalSource as FrameworkElement)?.DataContext;
//         if (item != null)
//         {
//             CommitSelection(item);
//         }
//     }
//
//     private void CommitSelection(object item)
//     {
//         var displayValue = GetPropertyValue(item, DisplayMemberPath);
//         var idValue = GetPropertyValue(item, SelectedValuePath);
//
//         Text = displayValue ?? string.Empty;
//         SelectedValue = idValue;
//
//         _inputTextBox.CaretIndex = _inputTextBox.Text.Length;
//         _suggestionPopup.IsOpen = false;
//     }
//
//     private void InputTextBox_LostFocus(object sender, RoutedEventArgs e)
//     {
//         if (_suggestionListBox.IsKeyboardFocusWithin) return;
//
//         ResolveSelectedValueOnLostFocus();
//         _suggestionPopup.IsOpen = false;
//     }
//
//     private void ResolveSelectedValueOnLostFocus()
//     {
//         if (ItemsSource == null) return;
//
//         var searchText = Text ?? string.Empty;
//
//         if (string.IsNullOrWhiteSpace(searchText))
//         {
//             SelectedValue = null;
//             return;
//         }
//
//         foreach (var item in ItemsSource)
//         {
//             var displayVal = GetPropertyValue(item, DisplayMemberPath);
//             if (string.Equals(displayVal, searchText, StringComparison.OrdinalIgnoreCase))
//             {
//                 SelectedValue = GetPropertyValue(item, SelectedValuePath);
//                 return;
//             }
//         }
//
//         SelectedValue = null;
//     }
//
//     private string? GetPropertyValue(object item, string propertyName)
//     {
//         if (item == null || string.IsNullOrEmpty(propertyName)) return item?.ToString();
//         var prop = item.GetType().GetProperty(propertyName);
//         return prop?.GetValue(item)?.ToString();
//     }
// }