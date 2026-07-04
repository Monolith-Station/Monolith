using System;
using System.Text.RegularExpressions;

namespace Content.Server._Goobstation.Administration;

/// <summary>
/// Utility methods for handling play time commands and time string parsing
/// </summary>
public static class PlayTimeCommandUtilities
{
    /// <summary>
    /// Parses a time string into minutes.
    /// </summary>
    /// <param name="timeString">Time string in a format like "1d 2h 30m" or "90m" or "1.5h"</param>
    /// <returns>The total number of minutes represented by the string</returns>
    public static double CountMinutes(string timeString)
    {
        if (string.IsNullOrWhiteSpace(timeString))
            return 0;

        double totalMinutes = 0;

        // Match patterns like "1d", "2h", "30m", "1.5h", etc.
        var dayMatch = Regex.Match(timeString, @"(\d+\.?\d*)d");
        var hourMatch = Regex.Match(timeString, @"(\d+\.?\d*)h");
        var minuteMatch = Regex.Match(timeString, @"(\d+\.?\d*)m");

        // Parse days
        if (dayMatch.Success && double.TryParse(dayMatch.Groups[1].Value, out var days))
            totalMinutes += days * 24 * 60;

        // Parse hours
        if (hourMatch.Success && double.TryParse(hourMatch.Groups[1].Value, out var hours))
            totalMinutes += hours * 60;

        // Parse minutes
        if (minuteMatch.Success && double.TryParse(minuteMatch.Groups[1].Value, out var minutes))
            totalMinutes += minutes;

        // If no specific unit is provided, assume it's minutes
        if (!dayMatch.Success && !hourMatch.Success && !minuteMatch.Success &&
            double.TryParse(timeString, out var plainMinutes))
            totalMinutes = plainMinutes;

        return totalMinutes;
    }
}