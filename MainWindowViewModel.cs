using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Input;

namespace Mantis_backdoor_scanner;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private const string ShowAllFilterOption = "Show All";
    private const string SortBySeverity = "Severity";
    private const string SortByAlphabetical = "Alphabetical";
    private const string SortByFilePath = "File Path";
    private const string SortByCategory = "Category";

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private string _selectedPath = string.Empty;
    private string _statusMessage = "Pick a file or folder to start.";
    private bool _isBusy;
    private string _filesScannedText = "0";
    private string _bytesScannedText = "0 bytes scanned";
    private string _warningCountText = "0";
    private string _categorySummary = "No issues found yet.";
    private string _detectedLanguagesText = "None";
    private string _skippedLanguagesText = "None";

    private string _detailRule = "Nothing selected";
    private string _detailSeverity = "-";
    private string _detailCategory = "-";
    private string _detailLine = "-";
    private string _detailSource = "-";
    private string _detailSnippet = "-";
    private ScanLanguageOption? _selectedScanLanguageOption;

    private string _selectedWarningCategory = ShowAllFilterOption;
    private string _selectedWarningSeverity = ShowAllFilterOption;
    private string _selectedWarningAlphabet = ShowAllFilterOption;
    private string _selectedWarningSort = SortBySeverity;
    private bool _warningsSortAscending = true;
    private FindingViewModel? _selectedFinding;

    private readonly RelayCommand _deleteSelectedWarningCommand;
    private readonly RelayCommand _showAllWarningsCommand;

    public MainWindowViewModel()
    {
        ScanLanguageOptions = Enum.GetValues<ScanLanguageChoice>()
            .Select(choice => new ScanLanguageOption(choice))
            .ToArray();
        _selectedScanLanguageOption = ScanLanguageOptions.First(option => option.Value == ScanLanguageChoice.AutoDetect);

        WarningSeverityOptions = [ShowAllFilterOption, "Extreme", "High", "Medium", "Low"];
        WarningSortOptions = [SortBySeverity, SortByAlphabetical, SortByFilePath, SortByCategory];
        WarningAlphabetOptions = new[] { ShowAllFilterOption }
            .Concat(Enumerable.Range('A', 26).Select(value => ((char)value).ToString()))
            .ToArray();

        WarningCategoryOptions.Add(ShowAllFilterOption);

        _deleteSelectedWarningCommand = new RelayCommand(_ => DeleteSelectedWarning(), _ => CanDeleteSelectedWarning);
        _showAllWarningsCommand = new RelayCommand(_ => ShowAllWarnings());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<FindingViewModel> Findings { get; } = new();

    public ObservableCollection<FindingViewModel> FilteredFindings { get; } = new();

    public ObservableCollection<LanguageSummaryViewModel> LanguageSummaries { get; } = new();

    public ObservableCollection<string> WarningCategoryOptions { get; } = new();

    public IReadOnlyList<string> WarningSeverityOptions { get; }

    public IReadOnlyList<string> WarningSortOptions { get; }

    public IReadOnlyList<string> WarningAlphabetOptions { get; }

    public IReadOnlyList<ScanLanguageOption> ScanLanguageOptions { get; }

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

    public string DetectedLanguagesText
    {
        get => _detectedLanguagesText;
        set => SetProperty(ref _detectedLanguagesText, value);
    }

    public string SkippedLanguagesText
    {
        get => _skippedLanguagesText;
        set => SetProperty(ref _skippedLanguagesText, value);
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

    public ScanLanguageOption? SelectedScanLanguageOption
    {
        get => _selectedScanLanguageOption;
        set
        {
            if (SetProperty(ref _selectedScanLanguageOption, value))
            {
                OnPropertyChanged(nameof(SelectedScanLanguage));
            }
        }
    }

    public ScanLanguageChoice SelectedScanLanguage => SelectedScanLanguageOption?.Value ?? ScanLanguageChoice.AutoDetect;

    public string SelectedWarningCategory
    {
        get => _selectedWarningCategory;
        set
        {
            if (SetProperty(ref _selectedWarningCategory, value))
            {
                RefreshFilteredFindings();
            }
        }
    }

    public string SelectedWarningSeverity
    {
        get => _selectedWarningSeverity;
        set
        {
            if (SetProperty(ref _selectedWarningSeverity, value))
            {
                RefreshFilteredFindings();
            }
        }
    }

    public string SelectedWarningAlphabet
    {
        get => _selectedWarningAlphabet;
        set
        {
            if (SetProperty(ref _selectedWarningAlphabet, value))
            {
                RefreshFilteredFindings();
            }
        }
    }

    public string SelectedWarningSort
    {
        get => _selectedWarningSort;
        set
        {
            if (SetProperty(ref _selectedWarningSort, value))
            {
                RefreshFilteredFindings();
            }
        }
    }

    public bool WarningsSortAscending
    {
        get => _warningsSortAscending;
        set
        {
            if (SetProperty(ref _warningsSortAscending, value))
            {
                RefreshFilteredFindings();
            }
        }
    }

    public FindingViewModel? SelectedFinding
    {
        get => _selectedFinding;
        set
        {
            if (SetProperty(ref _selectedFinding, value))
            {
                SetDetail(value);
                OnPropertyChanged(nameof(CanDeleteSelectedWarning));
                _deleteSelectedWarningCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool CanDeleteSelectedWarning => SelectedFinding is not null;

    public ICommand DeleteSelectedWarningCommand => _deleteSelectedWarningCommand;

    public ICommand ShowAllWarningsCommand => _showAllWarningsCommand;

    public bool CanScan => !_isBusy && !string.IsNullOrWhiteSpace(SelectedPath);

    public bool CanAnnihilate => !_isBusy && Findings.Count > 0 && !string.IsNullOrWhiteSpace(SelectedPath);

    public void SetBusy(bool busy)
    {
        _isBusy = busy;
        OnPropertyChanged(nameof(CanScan));
        OnPropertyChanged(nameof(CanAnnihilate));
    }

    private void DeleteSelectedWarning()
    {
        if (SelectedFinding is null)
        {
            return;
        }

        var selected = SelectedFinding;
        SelectedFinding = null;

        Findings.Remove(selected);

        RefreshWarningCategoryOptions();
        RefreshFilteredFindings();
        OnPropertyChanged(nameof(CanAnnihilate));
    }

    private void ShowAllWarnings()
    {
        SelectedWarningSeverity = ShowAllFilterOption;
        SelectedWarningCategory = ShowAllFilterOption;
        SelectedWarningAlphabet = ShowAllFilterOption;
    }

    private void RefreshFilteredFindings()
    {
        IEnumerable<FindingViewModel> query = Findings;

        if (!string.Equals(SelectedWarningCategory, ShowAllFilterOption, StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(item => string.Equals(item.Category, SelectedWarningCategory, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.Equals(SelectedWarningSeverity, ShowAllFilterOption, StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(item => string.Equals(NormalizeSeverity(item.Severity), NormalizeSeverity(SelectedWarningSeverity), StringComparison.Ordinal));
        }

        if (!string.Equals(SelectedWarningAlphabet, ShowAllFilterOption, StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(item => item.RuleName.StartsWith(SelectedWarningAlphabet, StringComparison.OrdinalIgnoreCase));
        }

        query = ApplyWarningSorting(query);

        var previousSelection = SelectedFinding;

        FilteredFindings.Clear();
        foreach (var item in query)
        {
            FilteredFindings.Add(item);
        }

        WarningCountText = FilteredFindings.Count.ToString("N0");

        if (previousSelection is not null && FilteredFindings.Contains(previousSelection))
        {
            if (!ReferenceEquals(SelectedFinding, previousSelection))
            {
                SelectedFinding = previousSelection;
            }
        }
        else
        {
            SelectedFinding = null;
        }
    }

    private IEnumerable<FindingViewModel> ApplyWarningSorting(IEnumerable<FindingViewModel> source)
    {
        return SelectedWarningSort switch
        {
            SortByAlphabetical => WarningsSortAscending
                ? source.OrderBy(item => item.RuleName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(item => item.Source, StringComparer.OrdinalIgnoreCase)
                : source.OrderByDescending(item => item.RuleName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(item => item.Source, StringComparer.OrdinalIgnoreCase),
            SortByFilePath => WarningsSortAscending
                ? source.OrderBy(item => item.Source, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(item => item.RuleName, StringComparer.OrdinalIgnoreCase)
                : source.OrderByDescending(item => item.Source, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(item => item.RuleName, StringComparer.OrdinalIgnoreCase),
            SortByCategory => WarningsSortAscending
                ? source.OrderBy(item => item.Category, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(item => item.RuleName, StringComparer.OrdinalIgnoreCase)
                : source.OrderByDescending(item => item.Category, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(item => item.RuleName, StringComparer.OrdinalIgnoreCase),
            _ => source.OrderByDescending(item => GetSeverityWeight(item.Severity))
                .ThenBy(item => item.RuleName, StringComparer.OrdinalIgnoreCase),
        };
    }

    private void RefreshWarningCategoryOptions()
    {
        var currentSelection = SelectedWarningCategory;

        WarningCategoryOptions.Clear();
        WarningCategoryOptions.Add(ShowAllFilterOption);

        foreach (var category in Findings
                     .Select(item => item.Category)
                     .Where(value => !string.IsNullOrWhiteSpace(value))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
        {
            WarningCategoryOptions.Add(category);
        }

        if (WarningCategoryOptions.Any(option => string.Equals(option, currentSelection, StringComparison.OrdinalIgnoreCase)))
        {
            _selectedWarningCategory = currentSelection;
            OnPropertyChanged(nameof(SelectedWarningCategory));
        }
        else
        {
            SelectedWarningCategory = ShowAllFilterOption;
        }
    }

    private static int GetSeverityWeight(string value)
    {
        return NormalizeSeverity(value) switch
        {
            "extreme" => 4,
            "high" => 3,
            "medium" => 2,
            "low" => 1,
            _ => 0,
        };
    }

    private static string NormalizeSeverity(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }

    public void ClearAll()
    {
        Findings.Clear();
        FilteredFindings.Clear();
        LanguageSummaries.Clear();

        WarningCategoryOptions.Clear();
        WarningCategoryOptions.Add(ShowAllFilterOption);

        SelectedWarningSeverity = ShowAllFilterOption;
        SelectedWarningCategory = ShowAllFilterOption;
        SelectedWarningAlphabet = ShowAllFilterOption;
        SelectedFinding = null;

        FilesScannedText = "0";
        BytesScannedText = "0 bytes scanned";
        WarningCountText = "0";
        CategorySummary = "No issues found yet.";
        DetectedLanguagesText = "None";
        SkippedLanguagesText = "None";
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
        BytesScannedText = FormatScannedBytes(interop.BytesScanned);

        if (parsed?.CategoryCounts is { Count: > 0 })
        {
            CategorySummary = string.Join(" | ", parsed.CategoryCounts.Select(kvp => $"{FormatWord(kvp.Key)}: {kvp.Value}"));
        }
        else
        {
            CategorySummary = "No issue groups found.";
        }

        LanguageSummaries.Clear();

        if (parsed?.LanguageSummaries is { Count: > 0 })
        {
            foreach (var summary in parsed.LanguageSummaries
                         .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
                         .Select(kvp => kvp.Value))
            {
                LanguageSummaries.Add(new LanguageSummaryViewModel
                {
                    Language = summary.Language ?? "Unknown",
                    FilesScanned = summary.FilesScanned,
                    Detections = summary.Detections,
                    SkippedFiles = summary.SkippedFiles
                });
            }

            var detected = LanguageSummaries.Where(item => item.FilesScanned > 0)
                .Select(item => item.Language)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            DetectedLanguagesText = detected.Length > 0 ? string.Join(", ", detected) : "None";
        }
        else
        {
            DetectedLanguagesText = "None";
        }

        if (parsed?.SkippedLanguages is { Count: > 0 })
        {
            SkippedLanguagesText = string.Join(", ", parsed.SkippedLanguages
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase));
        }
        else
        {
            SkippedLanguagesText = "None";
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

        RefreshWarningCategoryOptions();
        RefreshFilteredFindings();

        if (SelectedFinding is null && FilteredFindings.Count > 0)
        {
            SelectedFinding = FilteredFindings[0];
        }

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

    private static string FormatScannedBytes(ulong bytes)
    {
        const decimal oneThousand = 1000m;
        const decimal oneMillion = 1_000_000m;
        const decimal oneBillion = 1_000_000_000m;

        if (bytes < 1000)
        {
            return bytes == 1
                ? "1 byte scanned"
                : $"{bytes:N0} bytes scanned";
        }

        var decimalBytes = (decimal)bytes;

        if (decimalBytes < oneMillion)
        {
            return $"{decimalBytes / oneThousand:N2} KB scanned";
        }

        if (decimalBytes < oneBillion)
        {
            return $"{decimalBytes / oneMillion:N2} MB scanned";
        }

        return $"{decimalBytes / oneBillion:N2} GB scanned";
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

    private sealed class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter)
        {
            return _canExecute?.Invoke(parameter) ?? true;
        }

        public void Execute(object? parameter)
        {
            _execute(parameter);
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public sealed class ScanLanguageOption
    {
        public ScanLanguageOption(ScanLanguageChoice value)
        {
            Value = value;
            DisplayName = value.DisplayName();
        }

        public ScanLanguageChoice Value { get; }

        public string DisplayName { get; }
    }

    public sealed class LanguageSummaryViewModel
    {
        public string Language { get; set; } = string.Empty;
        public int FilesScanned { get; set; }
        public int Detections { get; set; }
        public int SkippedFiles { get; set; }
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

        [JsonPropertyName("language_summaries")]
        public Dictionary<string, LanguageSummaryDto>? LanguageSummaries { get; set; }

        [JsonPropertyName("skipped_languages")]
        public List<string>? SkippedLanguages { get; set; }

        [JsonPropertyName("findings")]
        public List<FileFindingDto>? Findings { get; set; }
    }

    public sealed class LanguageSummaryDto
    {
        [JsonPropertyName("language")]
        public string? Language { get; set; }

        [JsonPropertyName("files_scanned")]
        public int FilesScanned { get; set; }

        [JsonPropertyName("detections")]
        public int Detections { get; set; }

        [JsonPropertyName("skipped_files")]
        public int SkippedFiles { get; set; }
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
