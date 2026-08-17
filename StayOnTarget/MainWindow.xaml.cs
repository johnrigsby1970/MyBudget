using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using StayOnTarget.Models;
using StayOnTarget.ViewModels;
using StayOnTarget.Converters;
using System.Windows.Shapes;
using LiveChartsCore.SkiaSharpView.Painting;
using Serilog;
using SkiaSharp;
using StayOnTarget.Themes;

namespace StayOnTarget;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window {
    private readonly MainViewModel _viewModel = null!;

    public MainWindow() {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    public MainWindow(MainViewModel viewModel) : this()
    {
        _viewModel = viewModel;
        DataContext = _viewModel;
    }
    
    private void MainWindow_Loaded(object sender, RoutedEventArgs e) {
        try {
            _viewModel.PropertyChanged += Vm_PropertyChanged;
            // if (_viewModel.Accounts != null)
            //     UpdateProjectionColumns(_viewModel.Accounts);
        }
        catch (Exception ex) {
            Log.Error(ex, "Error in MainWindow_Loaded.");
        }
    }
    
    private void Vm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) {
        try {
            if ((e.PropertyName == nameof(MainViewModel.Accounts) || e.PropertyName == nameof(MainViewModel.VisibleAccounts)) && sender == DataContext) {
                if (DataContext is MainViewModel vm && vm.VisibleAccounts != null) {
                    UpdateProjectionColumns(vm.VisibleAccounts);
                    UpdateSnowballColumns(vm.VisibleAccounts);
                }
            }
        }
        catch (Exception ex) {
            Log.Error(ex, "Error in Vm_PropertyChanged for {PropertyName}.", e.PropertyName);
        }
    }

    private void UpdateProjectionColumns(IEnumerable<Account> accounts) {
        // Keep the first 5 columns (Date, Description, Amount, Total Balance, Period Net)
        while (ProjectionGrid.Columns.Count > 5) {
            ProjectionGrid.Columns.RemoveAt(5);
        }

        var sortedAccounts = accounts.OrderBy(a => a.Type switch {
            AccountType.Checking => 0,
            AccountType.CreditCard => 1,
            AccountType.Savings => 2,
            _ => 3
        }).ThenBy(a => a.Name);

        // 1. Fetch the WPF brush directly from resources
        var positiveBrush = System.Windows.Application.Current?.TryFindResource(ThemeKeys.PositiveBrush) as SolidColorBrush;
        Color positiveColor = positiveBrush?.Color ?? Colors.Green;

        // 2. Create native WPF LinearGradientBrush
        var positiveGradient = new LinearGradientBrush
        {
            StartPoint = new Point(0.5, 0),
            EndPoint = new Point(0.5, 1),
            GradientStops = new GradientStopCollection
            {
                new GradientStop(Color.FromArgb((byte)(255 * 0.35), positiveColor.R, positiveColor.G, positiveColor.B), 1.0),
                new GradientStop(Color.FromArgb((byte)(255 * 0.35), positiveColor.R, positiveColor.G, positiveColor.B), 1.0)
            }
        };
                        
        // 1. Fetch the WPF brush directly from resources
        var negativeBrush = System.Windows.Application.Current?.TryFindResource(ThemeKeys.NegativeBrush) as SolidColorBrush;
        Color negativeColor = negativeBrush?.Color ?? Colors.DarkRed;

        // 2. Create native WPF LinearGradientBrush
        var negativewpfGradient = new LinearGradientBrush
        {
            StartPoint = new Point(0.5, 0),
            EndPoint = new Point(0.5, 1),
            GradientStops = new GradientStopCollection
            {
                new GradientStop(Color.FromArgb((byte)(255 * 0.35), negativeColor.R, negativeColor.G, negativeColor.B), 1.0),
                new GradientStop(Color.FromArgb((byte)(255 * 0.35), negativeColor.R, negativeColor.G, negativeColor.B), 1.0)
            }
        };
        
        foreach (var account in sortedAccounts) {
            var accountName = account.Name;
            var accountId = account.Id;

            var column = new DataGridTemplateColumn {
                Header = accountName,
                Width = 110,
                IsReadOnly = true
            };

            var gridFactory = new FrameworkElementFactory(typeof(Grid));
            gridFactory.SetValue(Control.PaddingProperty, new Thickness(6, 0, 6, 0));

            var colDef1 = new FrameworkElementFactory(typeof(ColumnDefinition));
            colDef1.SetValue(ColumnDefinition.WidthProperty,
                new GridLength(16, GridUnitType.Pixel)); // Room for the shape
            var colDef2 = new FrameworkElementFactory(typeof(ColumnDefinition));
            colDef2.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));

            gridFactory.AppendChild(colDef1);
            gridFactory.AppendChild(colDef2);

            // 1. The Shape Container (Column 0)
            var shapeViewFactory = new FrameworkElementFactory(typeof(ContentControl));
            shapeViewFactory.SetValue(Grid.ColumnProperty, 0);
            shapeViewFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            shapeViewFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);

            // Dynamic Template Selector using ExpressionConverter to generate the right shape framework element
            shapeViewFactory.SetBinding(ContentControl.ContentProperty, new Binding(".") 
            {
                Converter = new ExpressionConverter<ProjectionItem, object>(item =>
                {
                    if (item.ToAccountId == accountId)
                    {
                        // INFLOW: Diamond shape (Square rotated 45 degrees)
                        var diamond = new Rectangle
                        {
                            Width = 8,
                            Height = 8,
                            Fill = positiveGradient, // Kept at your preferred 35%
                            RenderTransform = new System.Windows.Media.RotateTransform(45),
                            RenderTransformOrigin = new Point(0.5, 0.5),
                            // Left margin set to 4, top/right/bottom set to 0
                            Margin = new Thickness(4, 0, 0, 0) 
                        };
                        return diamond;
                    }
                    if (item.FromAccountId == accountId) {
                        // OUTFLOW: Standard Square
                        var square = new Rectangle
                        {
                            Width = 8,
                            Height = 8,
                            Fill = negativewpfGradient, // Kept at your preferred 35%
                            // Matching left margin of 4
                            Margin = new Thickness(4, 0, 0, 0) 
                        };
                        return square;
                    }

                    return null;
                })
            });
            gridFactory.AppendChild(shapeViewFactory);

            // 2. The Balance TextBlock (Column 1)
            var balanceFactory = new FrameworkElementFactory(typeof(TextBlock));
            balanceFactory.SetValue(Grid.ColumnProperty, 1);
            balanceFactory.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right);
            balanceFactory.SetBinding(TextBlock.TextProperty, new Binding(".") {
                Converter = new ExpressionConverter<ProjectionItem, string>(item =>
                    item.GetAccountBalance(accountName).ToString("C")),
            });
            gridFactory.AppendChild(balanceFactory);

            // 3. Apply Opacity and Font Weight (Ghosting Effect)
            gridFactory.SetBinding(Grid.OpacityProperty, new Binding(".") {
                Converter = new ExpressionConverter<ProjectionItem, double>(item =>
                    (item.ToAccountId == accountId || item.FromAccountId == accountId) ? 1.0 : 0.45)
            });

            gridFactory.SetBinding(Control.FontWeightProperty, new Binding(".") {
                Converter = new ExpressionConverter<ProjectionItem, FontWeight>(item =>
                    (item.ToAccountId == accountId || item.FromAccountId == accountId)
                        ? FontWeights.SemiBold
                        : FontWeights.Normal)
            });

            var template = new DataTemplate { VisualTree = gridFactory };
            column.CellTemplate = template;

            ProjectionGrid.Columns.Add(column);
        }
    }
    
        private void UpdateSnowballColumns(IEnumerable<Account> accounts) {
        // Keep the first 5 columns (Date, Description, Amount, Total Balance, Period Net)
        while (SnowballGrid.Columns.Count > 5) {
            SnowballGrid.Columns.RemoveAt(5);
        }

        var sortedAccounts = accounts.OrderBy(a => a.Type switch {
            AccountType.Checking => 0,
            AccountType.CreditCard => 1,
            AccountType.Savings => 2,
            _ => 3
        }).ThenBy(a => a.Name);

        foreach (var account in sortedAccounts) {
            var accountName = account.Name;
            var accountId = account.Id;

            var column = new DataGridTemplateColumn {
                Header = accountName,
                Width = 110,
                IsReadOnly = true
            };

            var gridFactory = new FrameworkElementFactory(typeof(Grid));
            gridFactory.SetValue(Control.PaddingProperty, new Thickness(6, 0, 6, 0));

            var colDef1 = new FrameworkElementFactory(typeof(ColumnDefinition));
            colDef1.SetValue(ColumnDefinition.WidthProperty,
                new GridLength(16, GridUnitType.Pixel)); // Room for the shape
            var colDef2 = new FrameworkElementFactory(typeof(ColumnDefinition));
            colDef2.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));

            gridFactory.AppendChild(colDef1);
            gridFactory.AppendChild(colDef2);

            // 1. The Shape Container (Column 0)
            var shapeViewFactory = new FrameworkElementFactory(typeof(ContentControl));
            shapeViewFactory.SetValue(Grid.ColumnProperty, 0);
            shapeViewFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            shapeViewFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);

            // Dynamic Template Selector using ExpressionConverter to generate the right shape framework element
            shapeViewFactory.SetBinding(ContentControl.ContentProperty, new Binding(".") 
            {
                Converter = new ExpressionConverter<ProjectionItem, object>(item =>
                {
                    if (item.ToAccountId == accountId)
                    {
                        // INFLOW: Diamond shape (Square rotated 45 degrees)
                        var diamond = new Rectangle
                        {
                            Width = 8,
                            Height = 8,
                            Fill = new SolidColorBrush(Colors.Green) { Opacity = 0.35 }, // Kept at your preferred 35%
                            RenderTransform = new System.Windows.Media.RotateTransform(45),
                            RenderTransformOrigin = new Point(0.5, 0.5),
                            // Left margin set to 4, top/right/bottom set to 0
                            Margin = new Thickness(4, 0, 0, 0) 
                        };
                        return diamond;
                    }
                    if (item.FromAccountId == accountId)
                    {
                        // OUTFLOW: Standard Square
                        var square = new Rectangle
                        {
                            Width = 8,
                            Height = 8,
                            Fill = new SolidColorBrush(Colors.DarkRed) { Opacity = 0.35 }, // Kept at your preferred 35%
                            // Matching left margin of 4
                            Margin = new Thickness(4, 0, 0, 0) 
                        };
                        return square;
                    }

                    return null;
                })
            });
            gridFactory.AppendChild(shapeViewFactory);

            // 2. The Balance TextBlock (Column 1)
            var balanceFactory = new FrameworkElementFactory(typeof(TextBlock));
            balanceFactory.SetValue(Grid.ColumnProperty, 1);
            balanceFactory.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right);
            balanceFactory.SetBinding(TextBlock.TextProperty, new Binding(".") {
                Converter = new ExpressionConverter<ProjectionItem, string>(item =>
                    item.GetAccountBalance(accountName).ToString("C")),
            });
            gridFactory.AppendChild(balanceFactory);

            // 3. Apply Opacity and Font Weight (Ghosting Effect)
            gridFactory.SetBinding(Grid.OpacityProperty, new Binding(".") {
                Converter = new ExpressionConverter<ProjectionItem, double>(item =>
                    (item.ToAccountId == accountId || item.FromAccountId == accountId) ? 1.0 : 0.45)
            });

            gridFactory.SetBinding(Control.FontWeightProperty, new Binding(".") {
                Converter = new ExpressionConverter<ProjectionItem, FontWeight>(item =>
                    (item.ToAccountId == accountId || item.FromAccountId == accountId)
                        ? FontWeights.SemiBold
                        : FontWeights.Normal)
            });

            var template = new DataTemplate { VisualTree = gridFactory };
            column.CellTemplate = template;

            SnowballGrid.Columns.Add(column);
        }
    }
        
    private void CloseOverlayOnBackdropClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.CloseManageExcludedAccountsCommand.Execute(null);
        }
    }
}