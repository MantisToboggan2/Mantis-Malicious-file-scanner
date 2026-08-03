using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mantis_backdoor_scanner;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private string _selectedPath = string.Empty;
    private string _statusMessage = "Pick a file or folder to start.";
    private bool _isBusy;
    private string _filesScannedText = "0";
    private string _bytesScannedText = "0";
    private string _warningCountText = "0";
    private string _categorySummary = "No issues found yet.";

    private string _detailRule = "Nothing selected";
    private string _detailSeverity = "-";
    private string _detailCategory = "-";
    private string _detailLine = "-";
    private string _detailSource = "-";
    private string _detailSnippet = "-";

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<FindingViewModel> Findings { get; } = new();

    public string SelectedPath
    {
        get => _selectedPath;
        set
        {
            if (SetProperty(ref _selectedPath, value))
            {
                OnPropertyChanged(nameof(CanScan));
                OnPropertyChanged(nameof(CanAnnihilate));
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string FilesScannedText
    {
        get => _filesScannedText;
        set => SetProperty(ref _filesScannedText, value);
    }

    public string BytesScannedText
    {
        get => _bytesScannedText;
        set => SetProperty(ref _bytesScannedText, value);
    }

    public string WarningCountText
    {
        get => _warningCountText;
        set => SetProperty(ref _warningCountText, value);
    }

    public string CategorySummary
    {
        get => _categorySummary;
        set => SetProperty(ref _categorySummary, value);
    }

    public string DetailRule
    {
        get => _detailRule;
        set => SetProperty(ref _detailRule, value);
    }

    public string DetailSeverity
    {
        get => _detailSeverity;
        set => SetProperty(ref _detailSeverity, value);
    }

    public string DetailCategory
    {
        get => _detailCategory;
        set => SetProperty(ref _detailCategory, value);
    }

    public string DetailLine
    {
        get => _detailLine;
        set => SetProperty(ref _detailLine, value);
    }

    public string DetailSource
    {
        get => _detailSource;
        set => SetProperty(ref _detailSource, value);
    }

    public string DetailSnippet
    {
        get => _detailSnippet;
        set => SetProperty(ref _detailSnippet, value);
    }

    public bool CanScan => !_isBusy && !string.IsNullOrWhiteSpace(SelectedPath);

    public bool CanAnnihilate => !_isBusy && Findings.Count > 0 && !string.IsNullOrWhiteSpace(SelectedPath);

    public void SetBusy(bool busy)
    {
        _isBusy = busy;
        OnPropertyChanged(nameof(CanScan));
        OnPropertyChanged(nameof(CanAnnihilate));
    }

    public void ClearAll()
    {
        Findings.Clear();
        FilesScannedText = "0";
        BytesScannedText = "0";
        WarningCountText = "0";
        CategorySummary = "No issues found yet.";
        StatusMessage = "Cleared.";
        SetDetail(null);
        OnPropertyChanged(nameof(CanAnnihilate));
    }

    public void ApplyInteropResult(RustScanResult interop)
    {
        var parsed = string.IsNullOrWhiteSpace(interop.Json)
            ? null
            : JsonSerializer.Deserialize<ScanResultDto>(interop.Json, _jsonOptions);

        Findings.Clear();

        FilesScannedText = interop.FilesScanned.ToString("N0");
        BytesScannedText = interop.BytesScanned.ToString("N0");
        WarningCountText = interop.DetectionCount.ToString("N0");

        if (parsed?.CategoryCounts is { Count: > 0 })
        {
            CategorySummary = string.Join(" | ", parsed.CategoryCounts.Select(kvp => $"{FormatWord(kvp.Key)}: {kvp.Value}"));
        }
        else
        {
            CategorySummary = "No issue groups found.";
        }

        if (parsed?.Findings is not null)
        {
            foreach (var file in parsed.Findings)
            {
                if (file.Matches is null)
                {
                    continue;
                }

                foreach (var match in file.Matches)
                {
                    Findings.Add(new FindingViewModel
                    {
                        RuleId = match.RuleId ?? string.Empty,
                        RuleName = match.RuleName ?? "Unknown warning",
                        Category = match.Category ?? "unknown",
                        Severity = match.Severity ?? "unknown",
                        Line = match.Line,
                        Snippet = match.Snippet ?? string.Empty,
                        Source = file.Source ?? string.Empty
                    });
                }
            }
        }

        SetDetail(Findings.FirstOrDefault());
        OnPropertyChanged(nameof(CanAnnihilate));
    }

    public void SetDetail(FindingViewModel? item)
    {
        if (item is null)
        {
            DetailRule = "Nothing selected";
            DetailSeverity = "-";
            DetailCategory = "-";
            DetailLine = "-";
            DetailSource = "-";
            DetailSnippet = "-";
            return;
        }

        DetailRule = $"{item.RuleName} ({item.RuleId})";
        DetailSeverity = FormatWord(item.Severity);
        DetailCategory = FormatWord(item.Category);
        DetailLine = item.Line.HasValue ? $"Line {item.Line.Value}" : "Line unknown";
        DetailSource = item.Source;
        DetailSnippet = string.IsNullOrWhiteSpace(item.Snippet) ? "-" : item.Snippet;
    }

    public static string FormatWord(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Unknown";
        }

        return string.Join(' ', value.Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => char.ToUpperInvariant(s[0]) + s[1..]));
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public sealed class FindingViewModel
    {
        public string RuleId { get; set; } = string.Empty;
        public string RuleName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public int? Line { get; set; }
        public string Snippet { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
    }

    public sealed class ScanResultDto
    {
        [JsonPropertyName("category_counts")]
        public Dictionary<string, int>? CategoryCounts { get; set; }

        [JsonPropertyName("findings")]
        public List<FileFindingDto>? Findings { get; set; }
    }

    public sealed class FileFindingDto
    {
        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("matches")]
        public List<RuleMatchDto>? Matches { get; set; }
    }

    public sealed class RuleMatchDto
    {
        [JsonPropertyName("rule_id")]
        public string? RuleId { get; set; }

        [JsonPropertyName("rule_name")]
        public string? RuleName { get; set; }

        [JsonPropertyName("category")]
        public string? Category { get; set; }

        [JsonPropertyName("severity")]
        public string? Severity { get; set; }

        [JsonPropertyName("line")]
        public int? Line { get; set; }

        [JsonPropertyName("snippet")]
        public string? Snippet { get; set; }
    }
}
