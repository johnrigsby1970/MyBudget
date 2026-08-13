using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using LiveChartsCore.SkiaSharpView.WPF;
using Serilog;
using SkiaSharp;
using StayOnTarget.Models;
using StayOnTarget.ViewModels;

namespace StayOnTarget;

public partial class ProjectionLiveChartControl : UserControl, INotifyPropertyChanged {
    // CHANGE: Instance variable instead of static
    private bool _isUpdatingFromSync = false;
    
    public static readonly DependencyProperty ProjectionsProperty =
        DependencyProperty.Register(nameof(Projections), typeof(ObservableCollection<ProjectionItem>),
            typeof(ProjectionLiveChartControl),
            new PropertyMetadata(null, OnDataChanged));

    public static readonly DependencyProperty AccountsProperty =
        DependencyProperty.Register(nameof(Accounts), typeof(ObservableCollection<Account>),
            typeof(ProjectionLiveChartControl),
            new PropertyMetadata(null, OnDataChanged));

    public ObservableCollection<ProjectionItem> Projections {
        get => (ObservableCollection<ProjectionItem>)GetValue(ProjectionsProperty);
        set => SetValue(ProjectionsProperty, value);
    }

    public ObservableCollection<Account> Accounts {
        get => (ObservableCollection<Account>)GetValue(AccountsProperty);
        set => SetValue(AccountsProperty, value);
    }

    private IEnumerable<ISeries> _series = Array.Empty<ISeries>();

    public IEnumerable<ISeries> Series {
        get => _series;
        set {
            _series = value;
            OnPropertyChanged(nameof(Series));
        }
    }

    public ObservableCollection<SeriesToggleItem> ToggleItems { get; } = new();

    // Delegate local IsSyncActive property directly to Global State
    public bool IsSyncActive {
        get => ProjectionFilterSyncManager.IsSyncEnabled;
        set {
            if (ProjectionFilterSyncManager.IsSyncEnabled != value) {
                ProjectionFilterSyncManager.IsSyncEnabled = value;
                
                // If the user just turned Sync ON from this view, 
                // immediately publish this chart's current state as ground truth
                if (value) {
                    BroadcastFilterState();
                }
            }
        }
    }
    
    // Group Toggle Properties
    private bool _isTotalBalanceVisible = true;

    public bool IsTotalBalanceVisible {
        get => _isTotalBalanceVisible;
        set {
            if (_isTotalBalanceVisible != value) {
                _isTotalBalanceVisible = value;
                OnPropertyChanged(nameof(IsTotalBalanceVisible));
                UpdateChart();
                BroadcastFilterState();
            }
        }
    }

    private bool _isLiquidGroupVisible = true;

    public bool IsLiquidGroupVisible {
        get => _isLiquidGroupVisible;
        set {
            if (_isLiquidGroupVisible != value) {
                _isLiquidGroupVisible = value;
                OnPropertyChanged(nameof(IsLiquidGroupVisible));
                SetGroupVisibility(IsLiquidAccount, value);
            }
        }
    }

    private bool _isCreditGroupVisible = true;

    public bool IsCreditGroupVisible {
        get => _isCreditGroupVisible;
        set {
            if (_isCreditGroupVisible != value) {
                _isCreditGroupVisible = value;
                OnPropertyChanged(nameof(IsCreditGroupVisible));
                SetGroupVisibility(IsCreditAccount, value);
            }
        }
    }

    private bool _isInvestmentsGroupVisible = true;

    public bool IsInvestmentsGroupVisible {
        get => _isInvestmentsGroupVisible;
        set {
            if (_isInvestmentsGroupVisible != value) {
                _isInvestmentsGroupVisible = value;
                OnPropertyChanged(nameof(IsInvestmentsGroupVisible));
                SetGroupVisibility(IsInvestmentAccount, value);
            }
        }
    }

    private static readonly SKColor LabelColor = SKColor.Parse("#94A3B8");
    private static readonly SKColor TotalBlue = SKColor.Parse("#38BDF8");

    public IEnumerable<Axis> XAxes { get; set; } = new[] {
        new Axis {
            LabelsPaint = new SolidColorPaint(GetLabelColor()),
            SeparatorsPaint = new SolidColorPaint(GetGridLineColor(), 1),
            Labeler = value => {
                if (double.IsNaN(value) || double.IsInfinity(value)) return string.Empty;
                var ticks = (long)value;
                if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks) return string.Empty;
                return new DateTime(ticks).ToString("M/d/yy");
            },
            LabelsRotation = 45,
            UnitWidth = TimeSpan.FromDays(1).Ticks
        }
    };

    public IEnumerable<Axis> YAxes { get; set; } = new[] {
        new Axis {
            LabelsPaint = new SolidColorPaint(GetLabelColor()),
            SeparatorsPaint = new SolidColorPaint(GetGridLineColor(), 1),
            Labeler = value => value.ToString("C0")
        }
    };

    private readonly CartesianChart _chart;

    public ProjectionLiveChartControl() {
        InitializeComponent();

        _chart = new CartesianChart {
            LegendPosition = LiveChartsCore.Measure.LegendPosition.Bottom,
            LegendTextPaint = new SolidColorPaint(LabelColor),
            Background = System.Windows.Media.Brushes.Transparent
        };

        ChartHostGrid.Children.Add(_chart);
        _chart.SetBinding(CartesianChart.SeriesProperty, new System.Windows.Data.Binding("Series") { Source = this });
        _chart.SetBinding(CartesianChart.XAxesProperty, new System.Windows.Data.Binding("XAxes") { Source = this });
        _chart.SetBinding(CartesianChart.YAxesProperty, new System.Windows.Data.Binding("YAxes") { Source = this });

        SeriesToggleItemsControl.ItemsSource = ToggleItems;
        SizeChanged += OnControlSizeChanged;

        // Register for Global Events
        ProjectionFilterSyncManager.OnFilterStateChanged += OnGlobalFilterStateChanged;
        ProjectionFilterSyncManager.OnSyncEnabledChanged += OnGlobalSyncEnabledChanged;

        Loaded += (s, e) => {
            OnPropertyChanged(nameof(IsSyncActive));
            if (IsSyncActive) {
                PullFromGlobalState();
            }
        };
    }

    // --- Sync Engine Methods ---

    private void OnGlobalSyncEnabledChanged(bool isEnabled) {
        OnPropertyChanged(nameof(IsSyncActive));
        if (isEnabled) {
            PullFromGlobalState();
        }
    }

    private void BroadcastFilterState() {
        if (_isUpdatingFromSync || !IsSyncActive) return;

        try {
            _isUpdatingFromSync = true;
            ProjectionFilterSyncManager.BroadcastState(IsTotalBalanceVisible, ToggleItems);
        }
        finally {
            _isUpdatingFromSync = false;
        }
    }

    // --- Classification Helpers ---

    private static bool IsLiquidAccount(AccountType type) => type switch {
        AccountType.Checking or AccountType.Savings or AccountType.Cash or AccountType.CD or AccountType.FDA => true,
        _ => false
    };

    private static bool IsCreditAccount(AccountType type) => type switch {
        AccountType.CreditCard or AccountType.PersonalLoan or AccountType.Auto or
            AccountType.StudentLoan or AccountType.HELOC or AccountType.OtherLiability => true,
        _ => false
    };

    private static bool IsInvestmentAccount(AccountType type) => type switch {
        AccountType.Investment or AccountType.Retirement401k or AccountType.Brokerage or
            AccountType.Mortgage or AccountType.RealEstate or AccountType.AppreciatingAsset or
            AccountType.CollegeFund or AccountType.IRA or AccountType.RothIRA or AccountType.Roth401k or
            AccountType.HSA or AccountType.Pension or AccountType.DigitalAsset or AccountType.OtherAsset or
            AccountType.RentalProperty or AccountType.Business => true,
        _ => false
    };

    private void OnGlobalFilterStateChanged() {
        if (!IsSyncActive || _isUpdatingFromSync) return;
        PullFromGlobalState();
    }

    private void PullFromGlobalState() {
        try {
            _isUpdatingFromSync = true;

            _isTotalBalanceVisible = ProjectionFilterSyncManager.IsTotalBalanceVisible;
            OnPropertyChanged(nameof(IsTotalBalanceVisible));

            foreach (var kvp in ProjectionFilterSyncManager.ToggleStates) {
                var localItem = ToggleItems.FirstOrDefault(t => t.Name == kvp.Key);
                if (localItem != null) {
                    localItem.SetIsVisibleQuietly(kvp.Value);
                }
            }

            SyncGroupStates();
            UpdateChart();
        }
        finally {
            _isUpdatingFromSync = false;
        }
    }

// Toggle Groups & Individual Items now call BroadcastFilterState()
    private void SetGroupVisibility(Func<AccountType, bool> predicate, bool isVisible) {
        if (_isUpdatingFromSync) return;

        bool changed = false;
        foreach (var item in ToggleItems) {
            if (predicate(item.Type) && item.IsVisible != isVisible) {
                item.IsVisible = isVisible;
                changed = true;
            }
        }

        if (changed) {
            SyncGroupStates();
            UpdateChart();
            BroadcastFilterState();
        }
    }

    private void SyncGroupStates() {
        _isLiquidGroupVisible = ToggleItems.Where(t => IsLiquidAccount(t.Type)).All(t => t.IsVisible);
        _isCreditGroupVisible = ToggleItems.Where(t => IsCreditAccount(t.Type)).All(t => t.IsVisible);
        _isInvestmentsGroupVisible = ToggleItems.Where(t => IsInvestmentAccount(t.Type)).All(t => t.IsVisible);

        OnPropertyChanged(nameof(IsLiquidGroupVisible));
        OnPropertyChanged(nameof(IsCreditGroupVisible));
        OnPropertyChanged(nameof(IsInvestmentsGroupVisible));
    }

    private void SyncToggleItems() {
        var accounts = Accounts?.Where(a => a.IncludeInTotal).ToList() ?? new List<Account>();
        var names = accounts.Select(a => a.Name).ToList();

        for (int i = ToggleItems.Count - 1; i >= 0; i--) {
            if (!names.Contains(ToggleItems[i].Name)) {
                ToggleItems.RemoveAt(i);
            }
        }

        foreach (var acc in accounts) {
            if (!ToggleItems.Any(t => t.Name == acc.Name)) {
                // If sync is active and global state exists, inherit initial state from global
                bool initialVisibility = IsSyncActive &&
                                         ProjectionFilterSyncManager.ToggleStates.TryGetValue(acc.Name, out var syncVal)
                    ? syncVal
                    : true;

                ToggleItems.Add(new SeriesToggleItem {
                    Name = acc.Name,
                    Type = acc.Type,
                    IsVisible = initialVisibility,
                    OnVisibilityChanged = () => {
                        if (!_isUpdatingFromSync) {
                            SyncGroupStates();
                            UpdateChart();
                            BroadcastFilterState();
                        }
                    }
                });
            }
        }

        SyncGroupStates();
    }

    private void UpdateChart() {
        try {
            if (Projections == null || !Projections.Any()) {
                Series = Array.Empty<ISeries>();
                return;
            }

            var projections = Projections.OrderBy(p => p.TransactionDate).ToList();
            var accounts = Accounts?.Where(a => a.IncludeInTotal).ToList() ?? new List<Account>();
            var seriesList = new List<ISeries>();

            // 1. Overall Total Balance Line
            if (IsTotalBalanceVisible) {
                var blueGradient = new SKColor[] { TotalBlue.WithAlpha(90), TotalBlue.WithAlpha(5) };
                seriesList.Add(new LineSeries<DateTimePoint> {
                    Name = "Total Balance",
                    Values = projections.Select(p => new DateTimePoint(p.TransactionDate, (double)p.Balance)).ToArray(),
                    Stroke = new SolidColorPaint(TotalBlue, 3),
                    Fill = new LinearGradientPaint(blueGradient, new SKPoint(0.5f, 0), new SKPoint(0.5f, 1)),
                    GeometrySize = 0,
                    LineSmoothness = 0.2
                });
            }

            // 2. Active Selected Accounts Subtotal
            var visibleAccounts = accounts
                .Where(a => ToggleItems.FirstOrDefault(t => t.Name == a.Name)?.IsVisible ?? false)
                .ToList();

            if (visibleAccounts.Count > 1) {
                SKColor selectedTotalColor = SKColor.Parse("#10B981");
                var blueGradient = new SKColor[] { selectedTotalColor.WithAlpha(90), selectedTotalColor.WithAlpha(5) };
                seriesList.Add(new LineSeries<DateTimePoint> {
                    Name = "Selected Total",
                    Values = projections.Select(p => {
                        double sum = visibleAccounts.Sum(acc => (double)p.GetAccountBalance(acc.Name));
                        return new DateTimePoint(p.TransactionDate, sum);
                    }).ToArray(),
                    Stroke = new SolidColorPaint(selectedTotalColor, 3) {
                        PathEffect = new DashEffect(new float[] { 6, 4 })
                    },
                    Fill = new LinearGradientPaint(blueGradient, new SKPoint(0.5f, 0), new SKPoint(0.5f, 1)),
                    GeometrySize = 0,
                    LineSmoothness = 0.2
                });
            }

            // 3. Individual Account Lines
            foreach (var acc in visibleAccounts) {
                var hex = acc.HexColor;
                if (string.IsNullOrWhiteSpace(hex)) hex = "#FF808080";
                if (!hex.StartsWith("#")) hex = "#" + hex;

                if (!SKColor.TryParse(hex, out var color)) {
                    color = SKColors.Gray;
                }

                seriesList.Add(new LineSeries<DateTimePoint> {
                    Name = acc.Name,
                    Values = projections.Select(p =>
                        new DateTimePoint(p.TransactionDate, (double)p.GetAccountBalance(acc.Name))).ToArray(),
                    Stroke = new SolidColorPaint(color, 2),
                    Fill = null,
                    GeometrySize = 0,
                    LineSmoothness = 0.2
                });
            }

            Series = seriesList.ToArray();
        }
        catch (Exception ex) {
            Log.Error(ex, "Error during UpdateChart.");
        }
    }

    private void OnSelectAllSeries(object sender, RoutedEventArgs e) {
        foreach (var item in ToggleItems) {
            item.IsVisible = true;
        }

        SyncGroupStates();
        UpdateChart();
        BroadcastFilterState();
    }

    private void OnClearAllSeries(object sender, RoutedEventArgs e) {
        foreach (var item in ToggleItems) {
            item.IsVisible = false;
        }

        SyncGroupStates();
        UpdateChart();
        BroadcastFilterState();
    }

    private void OnCloseDetailFilterPopup(object sender, RoutedEventArgs e) {
        DetailFilterToggleButton.IsChecked = false;
    }

    private void OnControlSizeChanged(object sender, SizeChangedEventArgs e) {
        const double minimumHeightForLegend = 350;
        const double minimumWidthForLegend = 500;

        _chart.LegendPosition = (e.NewSize.Width < minimumWidthForLegend || e.NewSize.Height < minimumHeightForLegend)
            ? LiveChartsCore.Measure.LegendPosition.Hidden
            : LiveChartsCore.Measure.LegendPosition.Bottom;
    }

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
        try {
            if (d is ProjectionLiveChartControl control) {
                if (e.OldValue is INotifyCollectionChanged oldCollection) {
                    oldCollection.CollectionChanged -= control.CollectionChanged;
                }

                if (e.NewValue is INotifyCollectionChanged newCollection) {
                    newCollection.CollectionChanged += control.CollectionChanged;
                }

                control.SyncToggleItems();
                control.UpdateChart();
            }
        }
        catch (Exception ex) {
            Log.Error(ex, "Error during OnDataChanged.");
        }
    }

    private void CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) {
        SyncToggleItems();
        UpdateChart();
    }

    private static SKColor GetGridLineColor() {
        if (Application.Current?.TryFindResource("GridLineBrush") is SolidColorBrush brush) {
            var c = brush.Color;
            return new SKColor(c.R, c.G, c.B, c.A);
        }

        return SKColor.Parse("#E2E8F0");
    }

    private static SKColor GetLabelColor() {
        if (Application.Current?.TryFindResource("SecondaryTextBrush") is SolidColorBrush brush) {
            var c = brush.Color;
            return new SKColor(c.R, c.G, c.B, c.A);
        }

        return SKColor.Parse("#64748B");
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName) {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}