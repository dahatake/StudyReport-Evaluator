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
        StringBuilder squeezedSource = new(source.Length);
        for (int index = 0; index < source.Length; index++)
        {
            if (!char.IsWhiteSpace(source[index]))
            {
                squeezedSource.Append(source[index]);
                originalIndexes.Add(index);
            }
        }

        string haystack = squeezedSource.ToString();
        int searchFrom = 0;
        int spanStart = -1;
        int spanEnd = -1;
        foreach (string fragment in evidence.Split(EllipsisTokens, StringSplitOptions.None))
        {
            string needle = RemoveWhitespace(fragment);
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

        exactEvidence = source[spanStart..spanEnd];
        return true;
    }

    private static string RemoveWhitespace(string value)
    {
        StringBuilder builder = new(value.Length);
        foreach (char character in value)
        {
            if (!char.IsWhiteSpace(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }
}
