using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FFBAnalyzer.Models;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace FFBAnalyzer.ViewModels;

/// <summary>Overlay and diff view for two or more runs of the same test type.</summary>
public partial class ComparisonViewModel : ObservableObject
{
    private static readonly SKColor[] Palette =
    {
        SKColors.DodgerBlue,
        SKColors.OrangeRed,
        SKColors.MediumSeaGreen,
        SKColors.Orchid,
        SKColors.Gold
    };

    private readonly MainViewModel _main;
    private readonly IReadOnlyList<Run> _allRuns;

    [ObservableProperty] private ObservableCollection<RunSelectionItem> _runItems = new();
    [ObservableProperty] private ObservableCollection<ISeries> _overlaySeries = new();
    [ObservableProperty] private ObservableCollection<ISeries> _diffSeries = new();
    [ObservableProperty] private Axis[] _xAxes;
    [ObservableProperty] private Axis[] _yAxes;
    [ObservableProperty] private ObservableCollection<MetricComparisonRow> _metricTable = new();

    public ComparisonViewModel(MainViewModel main, IReadOnlyList<Run> runs)
    {
        _main = main;
        _allRuns = runs;

        _xAxes = new[] { new Axis { Name = "Time (s)" } };
        _yAxes = new[] { new Axis { Name = "Force / Position" } };

        // Default: include all runs
        int idx = 0;
        foreach (var r in runs)
        {
            RunItems.Add(new RunSelectionItem(r, Palette[idx % Palette.Length], this));
            idx++;
        }

        Refresh();
    }

    public void Refresh()
    {
        var selected = RunItems.Where(ri => ri.IsIncluded).ToList();
        BuildOverlay(selected);
        BuildDiff(selected);
        BuildMetricTable(selected);
    }

    private void BuildOverlay(IReadOnlyList<RunSelectionItem> selected)
    {
        OverlaySeries = new ObservableCollection<ISeries>();
        foreach (var item in selected)
        {
            var times = item.Run.Data.GetTimeArray();
            // Prefer position if available, else commanded
            var values = item.Run.Data.GetPositionArray() ?? item.Run.Data.GetCommandedArray();
            var points = times.Zip(values)
                .Select(p => new LiveChartsCore.Defaults.ObservablePoint(p.First, p.Second))
                .ToList();

            OverlaySeries.Add(new LineSeries<LiveChartsCore.Defaults.ObservablePoint>
            {
                Name = item.Run.DisplayLabel,
                Values = points,
                Stroke = new SolidColorPaint(item.Color, 2),
                Fill = null,
                GeometrySize = 0,
                LineSmoothness = 0
            });
        }
    }

    private void BuildDiff(IReadOnlyList<RunSelectionItem> selected)
    {
        DiffSeries = new ObservableCollection<ISeries>();
        if (selected.Count < 2) return;

        var refRun = selected[0].Run;
        var refTimes = refRun.Data.GetTimeArray();
        var refValues = refRun.Data.GetPositionArray() ?? refRun.Data.GetCommandedArray();

        for (int i = 1; i < selected.Count; i++)
        {
            var cmp = selected[i].Run;
            var cmpValues = cmp.Data.GetPositionArray() ?? cmp.Data.GetCommandedArray();
            int n = Math.Min(refValues.Length, cmpValues.Length);

            var diffPoints = Enumerable.Range(0, n)
                .Select(j => new LiveChartsCore.Defaults.ObservablePoint(
                    refTimes[j],
                    cmpValues[j] - refValues[j]))
                .ToList();

            DiffSeries.Add(new LineSeries<LiveChartsCore.Defaults.ObservablePoint>
            {
                Name = $"{selected[i].Run.DisplayLabel} − {refRun.DisplayLabel}",
                Values = diffPoints,
                Stroke = new SolidColorPaint(selected[i].Color, 1.5f) { PathEffect = null },
                Fill = null,
                GeometrySize = 0,
                LineSmoothness = 0
            });
        }
    }

    private void BuildMetricTable(IReadOnlyList<RunSelectionItem> selected)
    {
        MetricTable = new ObservableCollection<MetricComparisonRow>();
        if (selected.Count == 0 || selected[0].Run.Metrics == null) return;

        var keys = selected
            .Where(s => s.Run.Metrics != null)
            .SelectMany(s => s.Run.Metrics!.Metrics.Select(m => m.Key))
            .Distinct()
            .ToList();

        foreach (var key in keys)
        {
            var row = new MetricComparisonRow { MetricKey = key };
            foreach (var item in selected)
            {
                var m = item.Run.Metrics?.Metrics.FirstOrDefault(x => x.Key == key);
                row.Values.Add(new RunMetricValue
                {
                    RunLabel = item.Run.DisplayLabel,
                    DisplayName = m?.DisplayName ?? key,
                    Value = m?.Value,
                    Unit = m?.Unit ?? string.Empty
                });
            }
            MetricTable.Add(row);
        }
    }

    [RelayCommand]
    public void GoBack() => _main.NavigateHome();
}

public partial class RunSelectionItem : ObservableObject
{
    private readonly ComparisonViewModel _parent;

    [ObservableProperty] private bool _isIncluded = true;

    partial void OnIsIncludedChanged(bool value) => _parent.Refresh();

    public Run Run { get; }
    public SKColor Color { get; }
    public string ColorHex => $"#{Color.Red:X2}{Color.Green:X2}{Color.Blue:X2}";

    public RunSelectionItem(Run run, SKColor color, ComparisonViewModel parent)
    {
        Run = run;
        Color = color;
        _parent = parent;
    }
}

public class MetricComparisonRow
{
    public string MetricKey { get; set; } = string.Empty;
    public List<RunMetricValue> Values { get; set; } = new();
}

public class RunMetricValue
{
    public string RunLabel { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public double? Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string Formatted => Value.HasValue ? $"{Value.Value:G4} {Unit}".Trim() : "–";
}
