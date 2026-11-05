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
        int searchFrom = 0;
        int spanStart = -1;
        int spanEnd = -1;
        foreach (string fragment in evidence.Split(EllipsisTokens, StringSplitOptions.None))
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

        while (spanEnd < source.Length && char.IsPunctuation(source[spanEnd]))
        {
            spanEnd++;
        }

        exactEvidence = source[spanStart..spanEnd];
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
                if (char.IsWhiteSpace(part) || char.IsPunctuation(part))
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
