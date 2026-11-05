using System.Text;

namespace StudyReportEvaluator.Core.Validation;

// Models routinely drop the space before a line break, join lines, or elide the middle with an ellipsis when they quote.
// Such a quote is accepted only when every fragment still appears, in order and ignoring whitespace, in the sent source.
// The accepted evidence is replaced by the exact contiguous source span, so stored evidence stays an exact substring.
internal static class EvidenceResolver
{
    private static readonly string[] EllipsisTokens = ["...", "…", "⋯"];

    public static bool TryResolve(string source, string evidence, out string exactEvidence)
    {
        exactEvidence = string.Empty;
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(evidence))
        {
            return false;
        }

        List<int> originalIndexes = new(source.Length);
        string haystack = Squeeze(source, originalIndexes);
        if (TryMatch(source, haystack, originalIndexes, evidence.Split(EllipsisTokens, StringSplitOptions.None), out exactEvidence))
        {
            return true;
        }

        // Models also rewrite the connectors between quoted pieces (for example "A / B" or "A→B" for "AとB").
        // Treat punctuation and symbols as fragment boundaries; every piece must still appear in order in the source.
        string[] pieces = evidence.Split(
            evidence.Where(character => char.IsPunctuation(character) || char.IsSymbol(character) || char.IsWhiteSpace(character)).Distinct().ToArray(),
            StringSplitOptions.RemoveEmptyEntries);
        if (pieces.Length > 1 && TryMatch(source, haystack, originalIndexes, pieces, out exactEvidence))
        {
            return true;
        }

        return TryMatchApproximately(source, haystack, originalIndexes, Squeeze(evidence, null), out exactEvidence);
    }

    // Models also silently correct a student's typo when quoting (for example "プログラミング" for the sent "プログミング").
    // A long quote may differ from one contiguous source span by at most one edit per CharactersPerAllowedEdit characters;
    // short quotes get no tolerance, so a quote of a different short phrase or a different cell is still rejected.
    private const int CharactersPerAllowedEdit = 20;

    private static bool TryMatchApproximately(
        string source,
        string haystack,
        List<int> originalIndexes,
        string needle,
        out string exactEvidence)
    {
        exactEvidence = string.Empty;
        int maxEdits = needle.Length / CharactersPerAllowedEdit;
        if (maxEdits == 0 || haystack.Length == 0)
        {
            return false;
        }

        // Sellers' approximate substring search: the match may start anywhere in the haystack (row 0 is all zeros),
        // and each cell carries the haystack index where its alignment started.
        int width = haystack.Length + 1;
        int[] previous = new int[width];
        int[] previousStart = new int[width];
        int[] current = new int[width];
        int[] currentStart = new int[width];
        for (int column = 0; column < width; column++)
        {
            previousStart[column] = column;
        }

        for (int row = 1; row <= needle.Length; row++)
        {
            current[0] = row;
            currentStart[0] = 0;
            for (int column = 1; column < width; column++)
            {
                int best = previous[column - 1] + (needle[row - 1] == haystack[column - 1] ? 0 : 1);
                int bestStart = previousStart[column - 1];
                if (previous[column] + 1 < best)
                {
                    best = previous[column] + 1;
                    bestStart = previousStart[column];
                }

                if (current[column - 1] + 1 < best)
                {
                    best = current[column - 1] + 1;
                    bestStart = currentStart[column - 1];
                }

                current[column] = best;
                currentStart[column] = bestStart;
            }

            (previous, current) = (current, previous);
            (previousStart, currentStart) = (currentStart, previousStart);
        }

        int bestEnd = -1;
        for (int column = 1; column < width; column++)
        {
            if (previous[column] <= maxEdits && (bestEnd < 0 || previous[column] < previous[bestEnd]))
            {
                bestEnd = column;
            }
        }

        if (bestEnd < 0 || previousStart[bestEnd] >= bestEnd)
        {
            return false;
        }

        int spanStart = originalIndexes[previousStart[bestEnd]];
        int spanEnd = originalIndexes[bestEnd - 1] + 1;
        exactEvidence = source[spanStart..ExtendOverTrailingPunctuation(source, spanEnd)];
        return true;
    }

    private static int ExtendOverTrailingPunctuation(string source, int spanEnd)
    {
        while (spanEnd < source.Length && char.IsPunctuation(source[spanEnd]))
        {
            spanEnd++;
        }

        return spanEnd;
    }

    private static bool TryMatch(string source, string haystack, List<int> originalIndexes, string[] fragments, out string exactEvidence)
    {
        exactEvidence = string.Empty;
        int searchFrom = 0;
        int spanStart = -1;
        int spanEnd = -1;
        foreach (string fragment in fragments)
        {
            string needle = Squeeze(fragment, null);
            if (needle.Length == 0)
            {
                continue;
            }

            int found = haystack.IndexOf(needle, searchFrom, StringComparison.Ordinal);
            if (found < 0)
            {
                return false;
            }

            if (spanStart < 0)
            {
                spanStart = originalIndexes[found];
            }

            int lastSqueezed = found + needle.Length - 1;
            spanEnd = originalIndexes[lastSqueezed] + 1;
            searchFrom = lastSqueezed + 1;
        }

        if (spanStart < 0)
        {
            return false;
        }

        exactEvidence = source[spanStart..ExtendOverTrailingPunctuation(source, spanEnd)];
        return true;
    }

    // Compatibility-normalizes each character (full-width/half-width digits, letters and parentheses)
    // and drops whitespace and punctuation, which models routinely rewrite when quoting Japanese text.
    private static string Squeeze(string value, List<int>? originalIndexes)
    {
        StringBuilder builder = new(value.Length);
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            string normalized = char.IsSurrogate(character)
                ? character.ToString()
                : character.ToString().Normalize(NormalizationForm.FormKC);
            foreach (char part in normalized)
            {
                if (char.IsWhiteSpace(part) || char.IsPunctuation(part) || char.IsSymbol(part))
                {
                    continue;
                }

                builder.Append(part);
                originalIndexes?.Add(index);
            }
        }

        return builder.ToString();
    }
}
