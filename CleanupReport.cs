using System;
using System.Collections.Generic;

namespace Mantis_backdoor_scanner;

public sealed record CleanupReport(
    string TargetPath,
    string BackupPath,
    string ReportPath,
    string Method,
    DateTimeOffset CreatedAt,
    string WhyItWasRemoved,
    string HowItWasDone,
    IReadOnlyList<CleanupReportItem> Items);

public sealed record CleanupReportItem(
    string FilePath,
    string RuleId,
    string RuleName,
    string Category,
    string Severity,
    int? Line,
    string Snippet,
    string Reason,
    string ActionTaken);
