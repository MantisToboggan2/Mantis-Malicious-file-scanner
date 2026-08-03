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
            return new CleanupSummary(targetPath, string.Empty, 0, 0, "Nothing to remove.");
        }

        var backupPath = CreateBackup(targetPath);

        if (Directory.Exists(targetPath))
        {
            var (changed, removed) = CleanFolderFindings(findings);
            return new CleanupSummary(targetPath, backupPath, changed, removed, BuildSummaryMessage(changed, removed));
        }

        if (targetPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            var (changed, removed) = CleanZip(targetPath, findings);
            return new CleanupSummary(targetPath, backupPath, changed, removed, BuildSummaryMessage(changed, removed));
        }

        var fileFindings = findings
            .Where(f => PathsEqual(f.Source, targetPath) && f.Line.HasValue)
            .ToList();

        var removedLines = RemoveMarkedLinesFromFile(targetPath, fileFindings.Select(f => f.Line!.Value));
        var changedFiles = removedLines > 0 ? 1 : 0;
        return new CleanupSummary(targetPath, backupPath, changedFiles, removedLines, BuildSummaryMessage(changedFiles, removedLines));
    }

    private static (int changedFiles, int removedLines) CleanFolderFindings(IReadOnlyCollection<MainWindowViewModel.FindingViewModel> findings)
    {
        var byFile = findings
            .Where(f => f.Line.HasValue)
            .GroupBy(f => f.Source, StringComparer.OrdinalIgnoreCase);

        var changedFiles = 0;
        var removedLines = 0;

        foreach (var group in byFile)
        {
            if (!File.Exists(group.Key))
            {
                continue;
            }

            var removed = RemoveMarkedLinesFromFile(group.Key, group.Select(f => f.Line!.Value));
            if (removed > 0)
            {
                changedFiles++;
                removedLines += removed;
            }
        }

        return (changedFiles, removedLines);
    }

    private static (int changedFiles, int removedLines) CleanZip(string zipPath, IReadOnlyCollection<MainWindowViewModel.FindingViewModel> findings)
    {
        var byEntry = findings
            .Where(f => f.Line.HasValue)
            .Select(f => ParseZipSource(zipPath, f.Source, f.Line!.Value))
            .Where(p => p is not null)
            .Cast<(string entryName, int line)>()
            .GroupBy(p => p.entryName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(x => x.line).ToHashSet(), StringComparer.OrdinalIgnoreCase);

        if (byEntry.Count == 0)
        {
            return (0, 0);
        }

        var tempPath = Path.Combine(Path.GetDirectoryName(zipPath)!, $"{Path.GetFileName(zipPath)}.tmp");
        var changedEntries = 0;
        var removedLines = 0;

        using (var sourceStream = File.OpenRead(zipPath))
        using (var sourceZip = new ZipArchive(sourceStream, ZipArchiveMode.Read))
        using (var targetStream = File.Create(tempPath))
        using (var targetZip = new ZipArchive(targetStream, ZipArchiveMode.Create))
        {
            foreach (var entry in sourceZip.Entries)
            {
                var newEntry = targetZip.CreateEntry(entry.FullName, CompressionLevel.Optimal);

                using var sourceEntryStream = entry.Open();
                using var reader = new MemoryStream();
                sourceEntryStream.CopyTo(reader);
                var data = reader.ToArray();

                if (byEntry.TryGetValue(entry.FullName, out var lines) && IsLuaLike(entry.FullName))
                {
                    var text = Encoding.UTF8.GetString(data);
                    var (updated, removed) = RemoveLinesFromText(text, lines);
                    if (removed > 0)
                    {
                        changedEntries++;
                        removedLines += removed;
                        data = Encoding.UTF8.GetBytes(updated);
                    }
                }

                using var destination = newEntry.Open();
                destination.Write(data, 0, data.Length);
            }
        }

        File.Delete(zipPath);
        File.Move(tempPath, zipPath);

        return (changedEntries, removedLines);
    }

    private static int RemoveMarkedLinesFromFile(string filePath, IEnumerable<int> lines)
    {
        if (!File.Exists(filePath))
        {
            return 0;
        }

        var text = File.ReadAllText(filePath);
        var (updated, removed) = RemoveLinesFromText(text, lines);
        if (removed == 0)
        {
            return 0;
        }

        File.WriteAllText(filePath, updated);
        return removed;
    }

    private static (string updatedText, int removedCount) RemoveLinesFromText(string text, IEnumerable<int> lines)
    {
        var removeSet = lines.Where(l => l > 0).ToHashSet();
        if (removeSet.Count == 0)
        {
            return (text, 0);
        }

        var allLines = text.Replace("\r\n", "\n").Split('\n');
        var output = new List<string>(allLines.Length);
        var removed = 0;

        for (var i = 0; i < allLines.Length; i++)
        {
            var lineNumber = i + 1;
            if (removeSet.Contains(lineNumber))
            {
                removed++;
                continue;
            }

            output.Add(allLines[i]);
        }

        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        return (string.Join(newline, output), removed);
    }

    private static string CreateBackup(string targetPath)
    {
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        if (Directory.Exists(targetPath))
        {
            var backup = $"{targetPath}_backup_{stamp}";
            CopyDirectory(targetPath, backup);
            return backup;
        }

        var fileInfo = new FileInfo(targetPath);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("Target not found", targetPath);
        }

        var backupFile = Path.Combine(fileInfo.DirectoryName!, $"{fileInfo.Name}.backup_{stamp}");
        File.Copy(targetPath, backupFile, overwrite: true);
        return backupFile;
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var name = Path.GetFileName(file);
            var dest = Path.Combine(destDir, name);
            File.Copy(file, dest, overwrite: true);
        }

        foreach (var directory in Directory.GetDirectories(sourceDir))
        {
            var name = Path.GetFileName(directory);
            CopyDirectory(directory, Path.Combine(destDir, name));
        }
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
}

public sealed record CleanupSummary(
    string TargetPath,
    string BackupPath,
    int ChangedFiles,
    int RemovedLines,
    string Message);
