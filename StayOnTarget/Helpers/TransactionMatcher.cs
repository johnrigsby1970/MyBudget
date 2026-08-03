using System.Text.RegularExpressions;
using FuzzySharp;

namespace StayOnTarget.Helpers;

public static class TransactionMatcher {
    // 1. Common prefixes injected by card processors
    private static readonly Regex PrefixNoise = new Regex(
        @"^(sq\s*\*|tst\s*\*|paypal\s*\*|pyp\s*\*|pos\s+|debit\s+|ckcard\s+)", 
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    // 2. Store numbers, order numbers, and hashes (#2920, Store 123, Ste 100, etc.)
    private static readonly Regex StoreAndLocationIdentifiers = new Regex(
        @"(#\d+|\b(store|no|ste|suite)\b\s*\d+)", 
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    // 3. State code at the end of string (e.g., " ORANGE PARK FL" -> removes " FL")
    private static readonly Regex TrailingStateCode = new Regex(
        @"\b[a-z]{2}$", 
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    // 4. Common business suffixes
    private static readonly Regex BusinessSuffixes = new Regex(
        @"\b(store|inc|co|llc|corp|corporation|com|net|org)\b", 
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private static readonly Regex NonAlphanumeric = new Regex(
        @"[^\w\s]", 
        RegexOptions.Compiled
    );

    private static readonly Regex MultipleSpaces = new Regex(
        @"\s+", 
        RegexOptions.Compiled
    );

    /// <summary>
    /// Normalizes raw transaction text down to the fundamental core merchant name.
    /// E.g., "WAL-MART #2920 ORANGE PARK FL" -> "walmart orange park"
    /// E.g., "SQ *LOCAL COFFEE CO" -> "local coffee"
    /// E.g., "Studio 54" -> "studio 54" or "RaceTrak 54" -> "racetrak 54" FuzzySharp will still match RaceTrak 585 with RaceTrak so we are not getting overly aggressive removing numbers.
    /// </summary>
    public static string NormalizeName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName)) 
            return string.Empty;

        // 1. Lowercase
        string result = rawName.ToLowerInvariant();

        // 2. Remove apostrophes BEFORE removing other punctuation (Lowe's -> lowes, Mojo's -> mojos)
        // 2. Remove apostrophes AND ampersands directly (AT&T -> att, Lowe's -> lowes)
        result = result.Replace("'", "")
            .Replace("’", "")
            .Replace("&", "");

        // 3. Remove store numbers (#2920, Store 123)
        result = StoreAndLocationIdentifiers.Replace(result, string.Empty);

        // 4. Replace remaining non-alphanumeric characters (like hyphens) with spaces
        result = NonAlphanumeric.Replace(result, " ");

        // 5. Remove trailing 2-letter state codes ONLY if preceded by another word
        // Prevent stripping standalone 2-letter names like "BP"
        result = Regex.Replace(result.Trim(), @"(?<=\s\w+)\s+[a-z]{2}$", "", RegexOptions.IgnoreCase);

        // 6. Remove business suffix words
        result = BusinessSuffixes.Replace(result, " ");

        // 7. Collapse spaces and trim
        return MultipleSpaces.Replace(result, " ").Trim();
    }

    /// <summary>
    /// Gets a match score between 0 and 100.
    /// Uses Token Set Ratio to prioritize core brand tokens.
    /// </summary>
    public static int GetMatchScore(string importedName, string manualName)
    {
        string cleanImported = NormalizeName(importedName);
        string cleanManual = NormalizeName(manualName);

        if (string.IsNullOrEmpty(cleanImported) || string.IsNullOrEmpty(cleanManual))
            return 0;

        // Instant exact match check
        if (cleanImported.Equals(cleanManual, StringComparison.OrdinalIgnoreCase))
            return 100;

        // FuzzySharp's TokenSetRatio handles overlapping word sets efficiently
        return Fuzz.TokenSetRatio(cleanImported, cleanManual);
    }

    public static bool IsMatch(string manual, string imported, int scoreThreshold = 80)
    {
        string cleanManual = NormalizeName(manual);
        string cleanImported = NormalizeName(imported);

        if (string.IsNullOrEmpty(cleanManual) || string.IsNullOrEmpty(cleanImported))
            return false;

        // Direct containment check
        if (cleanManual.Contains(cleanImported) || cleanImported.Contains(cleanManual))
            return true;

        // Fuzzy score comparison using FuzzySharp (generally outperforms pure Levenshtein for multi-word descriptions)
        int score = Fuzz.TokenSetRatio(cleanManual, cleanImported);
        return score >= scoreThreshold;
    }
}