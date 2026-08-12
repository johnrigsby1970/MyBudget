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
using LiveChartsCore.SkiaSharpView.WPF;
using Serilog;
using SkiaSharp;
using StayOnTarget.Models;
using StayOnTarget.ViewModels;

namespace StayOnTarget;

public partial class ProjectionLiveChartControl : UserControl, INotifyPropertyChanged {
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

    // Theme Colors for SkiaSharp Paints
    private static readonly SKColor LabelColor = SKColor.Parse("#94A3B8"); // SecondaryTextBrush Slate 400
    private static readonly SKColor SeparatorColor = SKColor.Parse("#1E293B"); // GridLineBrush Slate 800
    private static readonly SKColor TotalBlue = SKColor.Parse("#38BDF8"); // Vibrant Sky Blue

    public IEnumerable<Axis> XAxes { get; set; } = new[] {
        new Axis {
            LabelsPaint = new SolidColorPaint(GetLabelColor()),
            SeparatorsPaint = new SolidColorPaint(GetGridLineColor(), 1), // Soft 1px grid lines
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
            SeparatorsPaint = new SolidColorPaint(GetGridLineColor(), 1), // Soft 1px grid lines
            Labeler = value => value.ToString("C0")
        }
    };
    private readonly CartesianChart _chart;

    public ProjectionLiveChartControl() {
        InitializeComponent();
        
        _chart = new CartesianChart {
            LegendPosition = LiveChartsCore.Measure.LegendPosition.Bottom,
            // Style the Legend text for Dark Mode
            LegendTextPaint = new SolidColorPaint(LabelColor),
            // Make the Chart Background transparent to blend with your theme
            Background = System.Windows.Media.Brushes.Transparent
        };
        
        MainGrid.Children.Add(_chart);
        _chart.SetBinding(CartesianChart.SeriesProperty, new System.Windows.Data.Binding("Series") { Source = this });
        _chart.SetBinding(CartesianChart.XAxesProperty, new System.Windows.Data.Binding("XAxes") { Source = this });
        _chart.SetBinding(CartesianChart.YAxesProperty, new System.Windows.Data.Binding("YAxes") { Source = this });
        
        SizeChanged += OnControlSizeChanged;
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

                control.UpdateChart();
            }
        }
        catch (Exception ex) {
            Log.Error(ex, "Error during OnDataChanged.");
        }
    }

    private void CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) {
        UpdateChart();
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

            // Total Balance Gradient Fill (Pop Effect)
            var blueGradient = new SKColor[] { TotalBlue.WithAlpha(90), TotalBlue.WithAlpha(5) };

            // 1. Total Balance Line (Thicker, Vibrant Sky Blue with Area Gradient)
            seriesList.Add(new LineSeries<DateTimePoint> {
                Name = "Total Balance",
                Values = projections.Select(p => new DateTimePoint(p.TransactionDate, (double)p.Balance)).ToArray(),
                Stroke = new SolidColorPaint(TotalBlue, 3),
                Fill = new LinearGradientPaint(blueGradient, new SKPoint(0.5f, 0), new SKPoint(0.5f, 1)),
                GeometrySize = 0,
                LineSmoothness = 0.2
            });

            // 2. Individual Account Lines
            foreach (var acc in accounts) {
                SKColor color;
                var hex = acc.HexColor;
                if (string.IsNullOrWhiteSpace(hex)) hex = "#FF808080";
                if (!hex.StartsWith("#")) hex = "#" + hex;

                if (!SKColor.TryParse(hex, out color)) {
                    color = SKColors.Gray;
                }

                var paint = new SolidColorPaint(color, 2);

                seriesList.Add(new LineSeries<DateTimePoint> {
                    Name = acc.Name,
                    Values = projections.Select(p =>
                        new DateTimePoint(p.TransactionDate, (double)p.GetAccountBalance(acc.Name))).ToArray(),
                    Stroke = paint,
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

    // Get grid line color dynamically from theme or fallback to a soft light gray (#E2E8F0)
    private static SKColor GetGridLineColor() {
        if (Application.Current?.TryFindResource("GridLineBrush") is SolidColorBrush brush) {
            var c = brush.Color;
            return new SKColor(c.R, c.G, c.B, c.A);
        }
        return SKColor.Parse("#E2E8F0"); // Ultra-soft light mode grid lines
    }

    private static SKColor GetLabelColor() {
        if (Application.Current?.TryFindResource("SecondaryTextBrush") is SolidColorBrush brush) {
            var c = brush.Color;
            return new SKColor(c.R, c.G, c.B, c.A);
        }
        return SKColor.Parse("#64748B"); // Slate label text
    }
    
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName) {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}