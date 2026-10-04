using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SecApper.Security.Models;

namespace SecApper.Security.Ransomware;

public class ThreatScoringService : IThreatScoringService
{
    private static readonly HashSet<string> SuspiciousExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".locked", ".crypto", ".crypted", ".enc", ".crypt", ".wncry", ".stop", ".djvu",
        ".lockbit", ".blackcat", ".makop", ".phobos", ".mallox", ".medusa", ".akira",
        ".ransom", ".payme", ".vault", ".thor", ".locky", ".cerber", ".zepto"
    };

    public ThreatAssessment EvaluateEvents(IEnumerable<FileActivityEvent> events, ProtectionPolicy policy)
    {
        var eventList = events.ToList();
        if (eventList.Count == 0)
        {
            return new ThreatAssessment(0, ThreatLevel.Low, new List<string>(), 0);
        }

        int score = 0;
        var indicators = new List<string>();
        var affectedFiles = eventList.Select(e => e.FilePath).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        int affectedCount = affectedFiles.Count;

        // 1. Check for known ransomware extensions
        if (policy.ExtensionChangeProtection)
        {
            var suspiciousFileMatches = eventList.Where(e =>
            {
                string ext = Path.GetExtension(e.FilePath);
                return !string.IsNullOrEmpty(ext) && SuspiciousExtensions.Contains(ext);
            }).ToList();

            if (suspiciousFileMatches.Count > 0)
            {
                score += 50;
                indicators.Add($"Detected {suspiciousFileMatches.Count} file(s) with known ransomware extension(s) ({Path.GetExtension(suspiciousFileMatches[0].FilePath)})");
            }
        }

        // 2. Check for mass modifications
        int modCount = eventList.Count(e => e.ChangeType == WatcherChangeTypes.Changed);
        if (modCount >= policy.MassModificationThreshold)
        {
            score += 35;
            indicators.Add($"Mass file modification detected: {modCount} change events (threshold: {policy.MassModificationThreshold})");
        }

        // 3. Check for mass deletions
        if (policy.DeleteProtection)
        {
            int delCount = eventList.Count(e => e.ChangeType == WatcherChangeTypes.Deleted);
            if (delCount >= 10)
            {
                score += 35;
                indicators.Add($"Mass deletion detected: {delCount} files deleted rapidly");
            }
        }

        // 4. Check for mass renames
        if (policy.RenameProtection)
        {
            int renameCount = eventList.Count(e => e.ChangeType == WatcherChangeTypes.Renamed);
            if (renameCount >= 10)
            {
                score += 30;
                indicators.Add($"Mass rename detected: {renameCount} files renamed rapidly");
            }
        }

        // 5. Check event rate / density in short period
        if (eventList.Count >= 20)
        {
            var minTime = eventList.Min(e => e.Timestamp);
            var maxTime = eventList.Max(e => e.Timestamp);
            var duration = (maxTime - minTime).TotalSeconds;

            if (duration <= 3.0)
            {
                score += 25;
                indicators.Add($"Abnormal event burst rate: {eventList.Count} events in {duration:F1}s");
            }
        }

        // Cap score at 100
        score = Math.Min(100, score);

        ThreatLevel level = score switch
        {
            >= 75 => ThreatLevel.Critical,
            >= 50 => ThreatLevel.High,
            >= 25 => ThreatLevel.Medium,
            _ => ThreatLevel.Low
        };

        return new ThreatAssessment(score, level, indicators, affectedCount);
    }
}
