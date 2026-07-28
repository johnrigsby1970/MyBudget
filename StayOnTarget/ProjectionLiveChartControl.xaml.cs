using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WPF;
using Serilog;
using SkiaSharp;
using StayOnTarget.Models;
using StayOnTarget.ViewModels;

namespace StayOnTarget;

//https://github.com/Live-Charts/LiveCharts2?tab=MIT-1-ov-file#readme
//https://livecharts.dev/docs/WPF/2.0.0-rc6.1/samples.lines.straight
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

    public IEnumerable<Axis> XAxes { get; set; } = new[] {
        new Axis {
            Labeler = value => {
                // 1. Guard against NaN/Infinity
                if (double.IsNaN(value) || double.IsInfinity(value)) return string.Empty;

                // 2. Safely cast to long
                var ticks = (long)value;

                // 3. Ensure the tick value fits within valid DateTime boundaries
                if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks) {
                    return string.Empty;
                }

                return new DateTime(ticks).ToString("M/d/yy");
            },
            LabelsRotation = 45,
            UnitWidth = TimeSpan.FromDays(1).Ticks
        }
    };

    public IEnumerable<Axis> YAxes { get; set; } = new[] {
        new Axis {
            Labeler = value => value.ToString("C")
        }
    };

    private readonly CartesianChart _chart;

    public ProjectionLiveChartControl() {
        InitializeComponent();
        _chart = new CartesianChart {
            LegendPosition = LiveChartsCore.Measure.LegendPosition.Bottom
        };
        MainGrid.Children.Add(_chart);
        _chart.SetBinding(CartesianChart.SeriesProperty, new System.Windows.Data.Binding("Series") { Source = this });
        _chart.SetBinding(CartesianChart.XAxesProperty, new System.Windows.Data.Binding("XAxes") { Source = this });
        _chart.SetBinding(CartesianChart.YAxesProperty, new System.Windows.Data.Binding("YAxes") { Source = this });
        
        // Handle responsiveness on size changes
        SizeChanged += OnControlSizeChanged;
    }
    
    private void OnControlSizeChanged(object sender, SizeChangedEventArgs e) {
        // Choose your threshold width (e.g., 600px)
        const double minimumHeightForLegend = 500;
        const double minimumWidthForLegend = 500;

        if (e.NewSize.Width < minimumWidthForLegend || e.NewSize.Height < minimumHeightForLegend) {
            _chart.LegendPosition = LiveChartsCore.Measure.LegendPosition.Hidden;
        } else {
            _chart.LegendPosition = LiveChartsCore.Measure.LegendPosition.Bottom;
        }
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

            // Total Balance Series
            seriesList.Add(new LineSeries<DateTimePoint> {
                Name = "Total Balance",
                Values = projections.Select(p => new DateTimePoint(p.TransactionDate, (double)p.Balance)).ToArray(),
                Stroke = new SolidColorPaint(SKColors.DodgerBlue, 3),
                Fill = null,
                GeometrySize = 0,
                LineSmoothness = 0,
                GeometryStroke = new SolidColorPaint(SKColors.DodgerBlue, 3)
            });

            // Individual Account Series
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
                    LineSmoothness = 0,
                    GeometryStroke = paint
                    // GeometryFill = paint
                });
            }

            Series = seriesList.ToArray();
        }
        catch (Exception ex) {
            Log.Error(ex, "Error during UpdateChart.");
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName) {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}