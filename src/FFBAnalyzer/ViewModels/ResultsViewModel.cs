using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FFBAnalyzer.Models;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace FFBAnalyzer.ViewModels;

public partial class ResultsViewModel : ObservableObject
{
    private readonly MainViewModel _main;

    [ObservableProperty] private Run _run;
    [ObservableProperty] private ObservableCollection<ISeries> _chartSeries = new();
    [ObservableProperty] private Axis[] _xAxes;
    [ObservableProperty] private Axis[] _yAxes;
    [ObservableProperty] private ObservableCollection<Metric> _metrics = new();
    [ObservableProperty] private ObservableCollection<string> _interpretations = new();
    [ObservableProperty] private bool _hasPosition;

    public ResultsViewModel(MainViewModel main, Run run)
    {
        _main = main;
        _run = run;

        _xAxes = new[] { new Axis { Name = "Time (s)" } };
        _yAxes = new[] { new Axis { Name = "Force / Position" } };

        BuildChart();
        LoadMetrics();
    }

    private void BuildChart()
    {
        var times = Run.Data.GetTimeArray();
        var commanded = Run.Data.GetCommandedArray();
        var position = Run.Data.GetPositionArray();

        HasPosition = position != null;

        var commandedPoints = times.Zip(commanded, (t, f) => new { t, f })
            .Select(p => new LiveChartsCore.Defaults.ObservablePoint(p.t, p.f))
            .ToList();

        ChartSeries = new ObservableCollection<ISeries>
        {
            new LineSeries<LiveChartsCore.Defaults.ObservablePoint>
            {
                Name = "Commanded",
                Values = commandedPoints,
                Stroke = new SolidColorPaint(SKColors.DodgerBlue, 2),
                Fill = null,
                GeometrySize = 0,
                LineSmoothness = 0
            }
        };

        if (position != null)
        {
            var positionPoints = times.Zip(position, (t, p) => new { t, p })
                .Select(pt => new LiveChartsCore.Defaults.ObservablePoint(pt.t, pt.p))
                .ToList();

            ChartSeries.Add(new LineSeries<LiveChartsCore.Defaults.ObservablePoint>
            {
                Name = "Measured Position",
                Values = positionPoints,
                Stroke = new SolidColorPaint(SKColors.OrangeRed, 2),
                Fill = null,
                GeometrySize = 0,
                LineSmoothness = 0
            });
        }
    }

    private void LoadMetrics()
    {
        if (Run.Metrics == null) return;
        Metrics = new ObservableCollection<Metric>(Run.Metrics.Metrics);
        Interpretations = new ObservableCollection<string>(Run.Metrics.InterpretationSentences);
    }

    [RelayCommand]
    public async Task ExportCsvAsync(string filePath)
    {
        await _main.Exporter.ExportRunCsvAsync(Run, filePath);
    }

    [RelayCommand]
    public void GoBack() => _main.NavigateHome();
}
