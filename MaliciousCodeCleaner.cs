using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace Mantis_backdoor_scanner;

public sealed class MaliciousCodeCleaner
{
    public CleanupSummary Anihilate(string targetPath, IReadOnlyCollection<MainWindowViewModel.FindingViewModel> findings)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            throw new ArgumentException("Target path is required.", nameof(targetPath));
        }

        if (findings.Count == 0)
        {
            return new CleanupSummary(targetPath, string.Empty, string.Empty, 0, 0, "Nothing to remove.");
        }

        var stamp = DateTimeOffset.Now.ToString("yyyyMMdd_HHmmss");

        if (Directory.Exists(targetPath))
        {
            var changes = CollectFolderChanges(targetPath, findings);
            return FinalizeCleanup(targetPath, stamp, "folder", changes, ApplyFolderChanges, BuildFolderBackupPath, BuildFolderReportPath);
        }

        if (targetPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            var changes = CollectZipChanges(targetPath, findings);
            return FinalizeCleanup(targetPath, stamp, "archive", changes, ApplyZipChanges, BuildZipBackupPath, BuildZipReportPath);
        }

        var fileChanges = CollectFileChanges(targetPath, findings);
        return FinalizeCleanup(targetPath, stamp, "file", fileChanges, ApplyFileChanges, BuildFileBackupPath, BuildFileReportPath);
    }

    private static CleanupSummary FinalizeCleanup(
        string targetPath,
        string stamp,
        string cleanupKind,
        IReadOnlyList<CleanupChange> changes,
        Action<string, IReadOnlyList<CleanupChange>> applyChanges,
        Func<string, string, string> buildBackupPath,
        Func<string, string, string> buildReportPath)
    {
        if (changes.Count == 0)
        {
            return new CleanupSummary(targetPath, string.Empty, string.Empty, 0, 0, "No lines were removed.");
        }

        var changedFiles = changes.Count;
        var removedLines = changes.Sum(c => c.RemovedLines);

        var backupPath = buildBackupPath(targetPath, stamp);
        var reportPath = buildReportPath(targetPath, stamp);

        CreateBackupArchive(backupPath, changes);
        applyChanges(targetPath, changes);

        var report = BuildReport(targetPath, backupPath, reportPath, cleanupKind, changes, changedFiles, removedLines);
        SaveReport(reportPath, report);

        return new CleanupSummary(targetPath, backupPath, reportPath, changedFiles, removedLines, BuildSummaryMessage(changedFiles, removedLines));
    }

    private static List<CleanupChange> CollectFileChanges(string filePath, IReadOnlyCollection<MainWindowViewModel.FindingViewModel> findings)
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        var fileFindings = findings
            .Where(f => PathsEqual(f.Source, filePath) && f.Line.HasValue)
            .ToList();

        if (fileFindings.Count == 0)
        {
            return [];
        }

        var originalText = File.ReadAllText(filePath);
        var (updatedText, removedCount, removedLines) = RemoveLinesFromText(originalText, fileFindings.Select(f => f.Line!.Value));
        if (removedCount == 0)
        {
            return [];
        }

        return [new CleanupChange(
            SourcePath: filePath,
            BackupEntryName: MakeSafeEntryName($"changed-files/{Path.GetFileName(filePath)}.txt"),
            OriginalText: originalText,
            UpdatedText: updatedText,
            RemovedLines: removedCount,
            RemovedLineNumbers: removedLines,
            Items: BuildReportItems(fileFindings, filePath, removedLines))];
    }

    private static List<CleanupChange> CollectFolderChanges(string folderPath, IReadOnlyCollection<MainWindowViewModel.FindingViewModel> findings)
    {
        var changes = new List<CleanupChange>();

        foreach (var group in findings
                     .Where(f => f.Line.HasValue)
                     .GroupBy(f => f.Source, StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(group.Key))
            {
                continue;
            }

            var originalText = File.ReadAllText(group.Key);
            var (updatedText, removedCount, removedLines) = RemoveLinesFromText(originalText, group.Select(f => f.Line!.Value));
            if (removedCount == 0)
            {
                continue;
            }

            var relative = Path.GetRelativePath(folderPath, group.Key);
            var backupName = MakeSafeEntryName($"changed-files/{relative}.txt");
            changes.Add(new CleanupChange(
                SourcePath: group.Key,
                BackupEntryName: backupName,
                OriginalText: originalText,
                UpdatedText: updatedText,
                RemovedLines: removedCount,
                RemovedLineNumbers: removedLines,
                Items: BuildReportItems(group.ToList(), group.Key, removedLines)));
        }

        return changes;
    }

    private static List<CleanupChange> CollectZipChanges(string zipPath, IReadOnlyCollection<MainWindowViewModel.FindingViewModel> findings)
    {
        var byEntry = findings
            .Where(f => f.Line.HasValue)
            .Select(f => ParseZipSource(zipPath, f.Source, f.Line!.Value))
            .Where(p => p is not null)
            .Cast<(string entryName, int line)>()
            .GroupBy(p => p.entryName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(x => x.line).ToList(), StringComparer.OrdinalIgnoreCase);

        if (byEntry.Count == 0)
        {
            return [];
        }

        var changes = new List<CleanupChange>();

        using var sourceStream = File.OpenRead(zipPath);
        using var sourceZip = new ZipArchive(sourceStream, ZipArchiveMode.Read);

        foreach (var entry in sourceZip.Entries)
        {
            using var entryStream = entry.Open();
            using var memory = new MemoryStream();
            entryStream.CopyTo(memory);
            var originalBytes = memory.ToArray();
            var originalText = Encoding.UTF8.GetString(originalBytes);

            if (byEntry.TryGetValue(entry.FullName, out var lines) && IsLuaLike(entry.FullName))
            {
                var findingLines = lines.Distinct().ToArray();
                var (updatedText, removedCount, removedLines) = RemoveLinesFromText(originalText, findingLines);
                if (removedCount > 0)
                {
                    var entryFindings = findings
                        .Where(f => f.Line.HasValue && ParseZipSource(zipPath, f.Source, f.Line.Value)?.entryName.Equals(entry.FullName, StringComparison.OrdinalIgnoreCase) == true)
                        .ToList();

                    changes.Add(new CleanupChange(
                        SourcePath: $"{zipPath}::{entry.FullName}",
                        BackupEntryName: MakeSafeEntryName($"changed-entries/{entry.FullName}.txt"),
                        OriginalText: originalText,
                        UpdatedText: updatedText,
                        RemovedLines: removedCount,
                        RemovedLineNumbers: removedLines,
                        Items: BuildReportItems(entryFindings, $"{zipPath}::{entry.FullName}", removedLines)));
                }
            }
        }

        return changes;
    }

    private static void ApplyFileChanges(string filePath, IReadOnlyList<CleanupChange> changes)
    {
        if (changes.Count == 0)
        {
            return;
        }

        File.WriteAllText(filePath, changes[0].UpdatedText, Encoding.UTF8);
    }

    private static void ApplyFolderChanges(string folderPath, IReadOnlyList<CleanupChange> changes)
    {
        foreach (var change in changes)
        {
            File.WriteAllText(change.SourcePath, change.UpdatedText, Encoding.UTF8);
        }
    }

    private static void ApplyZipChanges(string zipPath, IReadOnlyList<CleanupChange> changes)
    {
        if (changes.Count == 0)
        {
            return;
        }

        var tempPath = Path.Combine(Path.GetDirectoryName(zipPath) ?? Environment.CurrentDirectory, $"{Path.GetFileNameWithoutExtension(zipPath)}.rewrite.tmp");
        var changeLookup = changes.ToDictionary(c => ParseZipSource(zipPath, c.SourcePath, 1)?.entryName ?? c.SourcePath, StringComparer.OrdinalIgnoreCase);

        using (var sourceStream = File.OpenRead(zipPath))
        using (var sourceZip = new ZipArchive(sourceStream, ZipArchiveMode.Read))
        using (var targetStream = File.Create(tempPath))
        using (var targetZip = new ZipArchive(targetStream, ZipArchiveMode.Create))
        {
            foreach (var entry in sourceZip.Entries)
            {
                var newEntry = targetZip.CreateEntry(entry.FullName, CompressionLevel.Optimal);

                using var sourceEntryStream = entry.Open();
                using var memory = new MemoryStream();
                sourceEntryStream.CopyTo(memory);
                var originalBytes = memory.ToArray();

                if (changeLookup.TryGetValue(entry.FullName, out var change))
                {
                    var updatedBytes = Encoding.UTF8.GetBytes(change.UpdatedText);
                    using var destination = newEntry.Open();
                    destination.Write(updatedBytes, 0, updatedBytes.Length);
                    continue;
                }

                using var carryForward = newEntry.Open();
                carryForward.Write(originalBytes, 0, originalBytes.Length);
            }
        }

        File.Delete(zipPath);
        File.Move(tempPath, zipPath);
    }

    private static void CreateBackupArchive(string backupPath, IReadOnlyList<CleanupChange> changes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(backupPath) ?? Environment.CurrentDirectory);

        using var stream = File.Create(backupPath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);

        foreach (var change in changes)
        {
            var entry = archive.CreateEntry(change.BackupEntryName, CompressionLevel.Optimal);
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write(change.OriginalText);
        }
    }

    private static string BuildFileBackupPath(string targetPath, string stamp)
    {
        var directory = Path.GetDirectoryName(targetPath) ?? Environment.CurrentDirectory;
        var name = Path.GetFileNameWithoutExtension(targetPath);
        return Path.Combine(directory, $"{name}.backup_{stamp}.zip");
    }

    private static string BuildFolderBackupPath(string targetPath, string stamp)
    {
        var parent = Directory.GetParent(targetPath)?.FullName ?? Environment.CurrentDirectory;
        var name = new DirectoryInfo(targetPath).Name;
        return Path.Combine(parent, $"{name}.backup_{stamp}.zip");
    }

    private static string BuildZipBackupPath(string targetPath, string stamp)
    {
        var directory = Path.GetDirectoryName(targetPath) ?? Environment.CurrentDirectory;
        var name = Path.GetFileNameWithoutExtension(targetPath);
        return Path.Combine(directory, $"{name}.backup_{stamp}.zip");
    }

    private static string BuildFileReportPath(string targetPath, string stamp)
    {
        var directory = Path.GetDirectoryName(targetPath) ?? Environment.CurrentDirectory;
        var name = Path.GetFileNameWithoutExtension(targetPath);
        return Path.Combine(directory, $"{name}.cleanup_report_{stamp}.txt");
    }

    private static string BuildFolderReportPath(string targetPath, string stamp)
    {
        var parent = Directory.GetParent(targetPath)?.FullName ?? Environment.CurrentDirectory;
        var name = new DirectoryInfo(targetPath).Name;
        return Path.Combine(parent, $"{name}.cleanup_report_{stamp}.txt");
    }

    private static string BuildZipReportPath(string targetPath, string stamp)
    {
        var directory = Path.GetDirectoryName(targetPath) ?? Environment.CurrentDirectory;
        var name = Path.GetFileNameWithoutExtension(targetPath);
        return Path.Combine(directory, $"{name}.cleanup_report_{stamp}.txt");
    }

    private static CleanupReport BuildReport(
        string targetPath,
        string backupPath,
        string reportPath,
        string cleanupKind,
        IReadOnlyList<CleanupChange> changes,
        int changedFiles,
        int removedLines)
    {
        var items = changes.SelectMany(c => c.Items).ToList();
        var why = removedLines > 0
            ? "The scanner found patterns that match known unsafe Lua behavior. Those items were removed to reduce the risk of dynamic execution, hidden hooks, unsafe file access, network calls, and obfuscated payloads."
            : "No suspicious lines were removed.";

        var how = $"{MainWindowViewModel.FormatWord(cleanupKind)} cleanup ran in a backup-first mode. The software created a ZIP archive containing text snapshots of the original changed files, removed only the flagged lines returned by the scanner, and then wrote this report for review.";

        return new CleanupReport(
            TargetPath: targetPath,
            BackupPath: backupPath,
            ReportPath: reportPath,
            Method: cleanupKind,
            CreatedAt: DateTimeOffset.Now,
            WhyItWasRemoved: why,
            HowItWasDone: how,
            Items: items);
    }

    private static void SaveReport(string reportPath, CleanupReport report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath) ?? Environment.CurrentDirectory);
        File.WriteAllText(reportPath, BuildTextReport(report), Encoding.UTF8);
    }

    private static string BuildTextReport(CleanupReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("MALICIOUS CODE REMOVAL REPORT");
        sb.AppendLine(new string('=', 32));
        sb.AppendLine();
        sb.AppendLine($"Created: {report.CreatedAt:yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine($"Target: {report.TargetPath}");
        sb.AppendLine($"Backup archive: {report.BackupPath}");
        sb.AppendLine($"Report file: {report.ReportPath}");
        sb.AppendLine($"Method: {report.Method}");
        sb.AppendLine();
        sb.AppendLine("WHY IT WAS REMOVED");
        sb.AppendLine(new string('-', 18));
        sb.AppendLine(report.WhyItWasRemoved);
        sb.AppendLine();
        sb.AppendLine("HOW IT WAS DONE");
        sb.AppendLine(new string('-', 15));
        sb.AppendLine(report.HowItWasDone);
        sb.AppendLine();
        sb.AppendLine($"REMOVED ITEMS ({report.Items.Count})");
        sb.AppendLine(new string('-', 18));

        foreach (var item in report.Items)
        {
            sb.AppendLine($"Rule: {item.RuleName} [{item.RuleId}]");
            sb.AppendLine($"File: {item.FilePath}");
            sb.AppendLine($"Group: {MainWindowViewModel.FormatWord(item.Category)}");
            sb.AppendLine($"Severity: {MainWindowViewModel.FormatWord(item.Severity)}");
            sb.AppendLine($"Line: {(item.Line.HasValue ? $"Line {item.Line.Value}" : "Unknown")}");
            sb.AppendLine($"Why: {item.Reason}");
            sb.AppendLine($"Action: {item.ActionTaken}");
            if (!string.IsNullOrWhiteSpace(item.Snippet))
            {
                sb.AppendLine($"Snippet: {item.Snippet}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static List<CleanupReportItem> BuildReportItems(
        IReadOnlyCollection<MainWindowViewModel.FindingViewModel> findings,
        string filePath,
        IReadOnlyCollection<int> lines)
    {
        var lineSet = lines.ToHashSet();
        return findings
            .Where(f => f.Line.HasValue && lineSet.Contains(f.Line.Value))
            .GroupBy(f => new { f.RuleId, f.RuleName, f.Category, f.Severity, f.Line, f.Snippet })
            .Select(g => new CleanupReportItem(
                FilePath: filePath,
                RuleId: g.Key.RuleId,
                RuleName: g.Key.RuleName,
                Category: g.Key.Category,
                Severity: g.Key.Severity,
                Line: g.Key.Line,
                Snippet: g.Key.Snippet,
                Reason: GetReason(g.Key.RuleId, g.Key.RuleName, g.Key.Category),
                ActionTaken: g.Key.Line.HasValue ? $"Removed line {g.Key.Line.Value} from {filePath}." : $"Reviewed {filePath}."))
            .ToList();
    }

    private static string GetReason(string ruleId, string ruleName, string category)
    {
        return ruleId switch
        {
            "R001" or "R002" or "R003" or "R004" => "This pattern can execute code dynamically or load code from text, which is often used to hide unsafe behavior.",
            "R005" or "R007" => "This pattern can change the runtime environment or access privileged functions, which can be used to bypass normal controls.",
            "R008" or "R009" => "This pattern can hook into debugging or callback systems in a way that hides behavior or intercepts execution.",
            "R010" or "R011" or "R012" => "This pattern can run shell commands or make outbound network requests, both of which can be used to contact remote services or launch other tools.",
            "R013" or "R014" => "This pattern can read, write, delete, or overwrite files, which may be used to scrape data or alter system content.",
            "R015" => "This pattern references a webhook endpoint, which is commonly used to send data to an external service.",
            "R016" or "R017" or "R018" or "R019" or "R020" or "R021" => "This pattern looks like obfuscated or encoded Lua content, which can hide the real intent of the script.",
            _ => $"This item matched the {MainWindowViewModel.FormatWord(category)} category and was treated as suspicious because it matched rule {ruleName}.",
        };
    }

    private static (string updatedText, int removedCount, IReadOnlyList<int> removedLines) RemoveLinesFromText(string text, IEnumerable<int> lines)
    {
        var removeSet = lines.Where(l => l > 0).ToHashSet();
        if (removeSet.Count == 0)
        {
            return (text, 0, Array.Empty<int>());
        }

        var allLines = text.Replace("\r\n", "\n").Split('\n');
        var output = new List<string>(allLines.Length);
        var removed = new List<int>();

        for (var i = 0; i < allLines.Length; i++)
        {
            var lineNumber = i + 1;
            if (removeSet.Contains(lineNumber))
            {
                removed.Add(lineNumber);
                continue;
            }

            output.Add(allLines[i]);
        }

        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        return (string.Join(newline, output), removed.Count, removed);
    }

    private static string MakeSafeEntryName(string entryName)
    {
        var invalid = Path.GetInvalidFileNameChars().Concat(Path.GetInvalidPathChars()).Distinct().ToHashSet();
        var builder = new StringBuilder(entryName.Length);

        foreach (var ch in entryName)
        {
            if (ch == '/' || ch == '\\')
            {
                builder.Append('/');
            }
            else if (invalid.Contains(ch) || ch == ':')
            {
                builder.Append('_');
            }
            else
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }

    private static (string entryName, int line)? ParseZipSource(string zipPath, string source, int line)
    {
        var prefix = zipPath + "::";
        return source.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? (source[prefix.Length..], line)
            : null;
    }

    private static bool IsLuaLike(string path)
    {
        var ext = Path.GetExtension(path);
        return ext.Equals(".lua", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".luau", StringComparison.OrdinalIgnoreCase);
    }

    private static bool PathsEqual(string left, string right)
    {
        return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildSummaryMessage(int changedFiles, int removedLines)
    {
        if (changedFiles == 0 || removedLines == 0)
        {
            return "No lines were removed.";
        }

        return $"Removed {removedLines:N0} suspicious line(s) across {changedFiles:N0} file(s).";
    }

    private sealed record CleanupChange(
        string SourcePath,
        string BackupEntryName,
        string OriginalText,
        string UpdatedText,
        int RemovedLines,
        IReadOnlyList<int> RemovedLineNumbers,
        IReadOnlyList<CleanupReportItem> Items);
}

public sealed record CleanupSummary(
    string TargetPath,
    string BackupPath,
    string ReportPath,
    int ChangedFiles,
    int RemovedLines,
    string Message);
