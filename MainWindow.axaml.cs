using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Mantis_backdoor_scanner;

public partial class MainWindow : Window
{
    private readonly RustScannerInterop _interop = new();
    private readonly MaliciousCodeCleaner _cleaner = new();

    public MainWindowViewModel ViewModel { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = ViewModel;
    }

    private async void PickFileArchiveButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Choose a file or archive",
            FileTypeFilter =
            [
                new FilePickerFileType("Supported code files")
                {
                    Patterns = ["*.lua", "*.luau", "*.cpp", "*.cxx", "*.cc", "*.hpp", "*.hxx", "*.hh", "*.h", "*.rs", "*.cs", "*.dll", "*.exe", "*.winmd", "*.e2", "*.zip"]
                },
                FilePickerFileTypes.All
            ]
        });

        var file = files.FirstOrDefault();
        if (file is null)
        {
            return;
        }

        ViewModel.SelectedPath = file.TryGetLocalPath() ?? file.Path.LocalPath;
        ViewModel.StatusMessage = "Item selected. Click Start scan.";
    }

    private async void PickFolderButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Choose a folder"
        });

        var folder = folders.FirstOrDefault();
        if (folder is null)
        {
            return;
        }

        ViewModel.SelectedPath = folder.TryGetLocalPath() ?? folder.Path.LocalPath;
        ViewModel.StatusMessage = "Folder selected. Click Start scan.";
    }

    private async void ScanButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!ViewModel.CanScan)
        {
            return;
        }

        ViewModel.SetBusy(true);
        ViewModel.StatusMessage = ViewModel.SelectedScanLanguage == ScanLanguageChoice.AutoDetect
            ? "Scanning files..."
            : $"Scanning files as {ViewModel.SelectedScanLanguage.DisplayName()}...";

        try
        {
            var path = ViewModel.SelectedPath.Trim();
            var result = await Task.Run(() => _interop.ScanPath(path, ViewModel.SelectedScanLanguage));

            if (!result.Success)
            {
                ViewModel.StatusMessage = string.IsNullOrWhiteSpace(result.Error)
                    ? "Scan could not finish."
                    : $"Scan could not finish: {result.Error}";
                return;
            }

            ViewModel.ApplyInteropResult(result);
            ViewModel.StatusMessage = $"Done. Found {ViewModel.WarningCountText} warning(s).";
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = $"Scan failed: {ex.Message}";
        }
        finally
        {
            ViewModel.SetBusy(false);
        }
    }

    private void ClearButton_OnClick(object? sender, RoutedEventArgs e)
    {
        ViewModel.ClearAll();
    }

    private async void AnnihilateButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!ViewModel.CanAnnihilate)
        {
            return;
        }

        var approve = await ShowConfirmDialogAsync(
            "Annihilate malicious code",
            "This will create a backup and remove flagged Lua lines from the selected target. Continue?");

        if (!approve)
        {
            ViewModel.StatusMessage = "Cleanup canceled.";
            return;
        }

        ViewModel.SetBusy(true);
        ViewModel.StatusMessage = "Removing flagged code...";

        try
        {
            var findings = ViewModel.Findings.ToList();
            var summary = await Task.Run(() => _cleaner.Anihilate(ViewModel.SelectedPath, findings));

            ViewModel.StatusMessage = string.IsNullOrWhiteSpace(summary.ReportPath)
                ? "Cleanup complete. No report was needed. Run scan again to verify."
                : "Cleanup complete. Report saved. Run scan again to verify.";
            await ShowInfoDialogAsync(
                "Cleanup Summary",
                $"Backup: {summary.BackupPath}\nReport: {(string.IsNullOrWhiteSpace(summary.ReportPath) ? "No report was created." : summary.ReportPath)}\n\n{summary.Message}\n\nChanged files: {summary.ChangedFiles}\nRemoved lines: {summary.RemovedLines}");
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = $"Cleanup failed: {ex.Message}";
            await ShowInfoDialogAsync("Cleanup Failed", ex.Message);
        }
        finally
        {
            ViewModel.SetBusy(false);
        }
    }

    private void FindingsListBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox listBox)
        {
            return;
        }

        ViewModel.SetDetail(listBox.SelectedItem as MainWindowViewModel.FindingViewModel);
    }

    private async Task<bool> ShowConfirmDialogAsync(string title, string message)
    {
        var result = false;
        var dialog = new Window
        {
            Width = 520,
            Height = 230,
            Title = title,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#0F172A")),
            Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E6EEF9"))
        };

        dialog.Content = BuildDialogContent(dialog, message, accepted =>
        {
            result = accepted;
        });

        await dialog.ShowDialog(this);
        return result;
    }

    private async Task ShowInfoDialogAsync(string title, string message)
    {
        var dialog = new Window
        {
            Width = 620,
            Height = 280,
            Title = title,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#0F172A")),
            Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E6EEF9"))
        };

        dialog.Content = BuildDialogContent(dialog, message, _ => { }, showCancel: false);

        await dialog.ShowDialog(this);
    }

    private static Control BuildDialogContent(Window dialog, string message, Action<bool> closeAction, bool showCancel = true)
    {
        var text = new TextBlock
        {
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Margin = new Avalonia.Thickness(0, 0, 0, 16)
        };

        var okButton = new Button
        {
            Content = showCancel ? "Yes, Continue" : "OK",
            Width = 120,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 100,
            IsVisible = showCancel
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { cancelButton, okButton }
        };

        var panel = new StackPanel
        {
            Margin = new Avalonia.Thickness(18),
            Spacing = 8,
            Children = { text, buttons }
        };

        var host = new Border
        {
            BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#2A4673")),
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(10),
            Padding = new Avalonia.Thickness(10),
            Child = panel
        };

        okButton.Click += (_, _) =>
        {
            closeAction(true);
            dialog.Close();
        };

        cancelButton.Click += (_, _) =>
        {
            closeAction(false);
            dialog.Close();
        };

        return host;
    }
}
