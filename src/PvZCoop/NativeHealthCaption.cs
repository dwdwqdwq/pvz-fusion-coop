using System.Text.RegularExpressions;

namespace PvZCoop;

// Native body/armor counters remain in their original format. In particular,
// localization must not swap the font/material while their digits are redrawn.
public static class NativeHealthCaption
{
    static readonly Regex format = new(@"^(?:Tier \d+\r?\n)?HP[:：]\s*-?\d+/\d+", RegexOptions.CultureInvariant);
    public static bool Matches(string value) => !string.IsNullOrEmpty(value) && format.IsMatch(value);
}
