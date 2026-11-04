using System.Collections.Immutable;
using System.Globalization;

namespace StudyReportEvaluator.Core.Similarity;

/// <summary>
/// FR-114: 参照解答との表面類似度を「確認指標（未検証）」として扱うための共通定義。
/// 確率・閾値・不正判定は持たない。短文注意（PARAM-004）は表示上の注意であり、判定や減点の閾値ではない。
/// </summary>
public static class SimilarityDiagnostics
{
    /// <summary>PARAM-004: 短文注意を付ける正規化後全文長の上限（以下なら注意）。</summary>
    public const int ShortTextCautionMaximumLength = 30;

    /// <summary>FR-114 AC-078: 通常評価と区別するための表示ラベル。</summary>
    public const string IndicatorLabel = "確認指標・未検証";

    /// <summary>FR-114 AC-079: 旧形式の結果で、参照別の詳細が保存されていないときの表示。</summary>
    public const string LegacyDetailLabel = "旧形式・未取得";

    /// <summary>FR-114 AC-078: 結果画面と workbook に添える説明（判定・確率・断定を含まない）。</summary>
    public const string IndicatorNotice =
        "類似度は参照解答との表面的な文字列の重なりを示す確認指標・未検証の値です。通常評価とは別の項目で、AI 使用や不正の有無を示すものではありません。";

    /// <summary>いずれかの正規化後全文長が PARAM-004 以下なら短文注意（空の側も短文として扱う）。</summary>
    public static bool IsShortText(int studentNormalizedLength, int referenceNormalizedLength) =>
        Math.Min(studentNormalizedLength, referenceNormalizedLength) <= ShortTextCautionMaximumLength;

    /// <summary>
    /// FR-114 AC-010 / AC-011: 減点に使う代表値は、完全な参照集合の参照別合成値の最大値。
    /// 同点なら参照番号の小さいものを選ぶ。空なら null。
    /// </summary>
    public static SimilarityReferenceDiagnostic? SelectMaximum(IEnumerable<SimilarityReferenceDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        SimilarityReferenceDiagnostic? best = null;
        foreach (SimilarityReferenceDiagnostic diagnostic in diagnostics)
        {
            if (diagnostic is null)
            {
                continue;
            }

            if (best is null
                || diagnostic.Score > best.Score
                || (diagnostic.Score == best.Score && diagnostic.ReferenceIndex < best.ReferenceIndex))
            {
                best = diagnostic;
            }
        }

        return best;
    }

    /// <summary>画面表示用の 1 行要約（学生本文・参照本文は含めない）。</summary>
    public static string Describe(SimilarityReferenceDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        string exact = diagnostic.IsRawExactMatch
            ? "原文全文一致"
            : diagnostic.IsNormalizedExactMatch ? "正規化後全文一致" : "全文一致なし";
        string position = diagnostic.StudentNormalizedMatchStart < 0
            ? "一致箇所なし"
            : string.Create(
                CultureInfo.InvariantCulture,
                $"一致位置（正規化後）学生 {diagnostic.StudentNormalizedMatchStart} / 参照 {diagnostic.ReferenceNormalizedMatchStart}");
        string caution = diagnostic.IsShortTextCaution ? " / 短文注意" : string.Empty;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{IndicatorLabel}: 参照 {diagnostic.ReferenceIndex} / {exact} / 最長一致 {diagnostic.LongestCommonSubstringLength}文字 / 被覆率 学生 {diagnostic.StudentCoverage:0.00}・参照 {diagnostic.ReferenceCoverage:0.00} / {position}{caution}");
    }
}

/// <summary>
/// FR-114 AC-010 / AC-077: 1 つの参照解答に対する確認指標の詳細。学生本文・参照本文は保持しない（位置と長さだけ）。
/// </summary>
public sealed record SimilarityReferenceDiagnostic
{
    /// <summary>参照集合の中の 1 起点の番号。</summary>
    public int ReferenceIndex { get; init; }

    public decimal Score { get; init; }

    public bool IsRawExactMatch { get; init; }

    public bool IsNormalizedExactMatch { get; init; }

    public int LongestCommonSubstringLength { get; init; }

    public decimal StudentCoverage { get; init; }

    public decimal ReferenceCoverage { get; init; }

    public int StudentNormalizedMatchStart { get; init; } = -1;

    public int ReferenceNormalizedMatchStart { get; init; } = -1;

    public int StudentNormalizedLength { get; init; }

    public int ReferenceNormalizedLength { get; init; }

    public bool IsShortTextCaution { get; init; }

    public static SimilarityReferenceDiagnostic From(int referenceIndex, SurfaceSimilarityResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (referenceIndex < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(referenceIndex));
        }

        return new SimilarityReferenceDiagnostic
        {
            ReferenceIndex = referenceIndex,
            Score = result.Score,
            IsRawExactMatch = result.IsRawExactMatch,
            IsNormalizedExactMatch = result.IsNormalizedExactMatch,
            LongestCommonSubstringLength = result.LongestCommonSubstringLength,
            StudentCoverage = result.StudentCoverage,
            ReferenceCoverage = result.ReferenceCoverage,
            StudentNormalizedMatchStart = result.StudentNormalizedMatchStart,
            ReferenceNormalizedMatchStart = result.ReferenceNormalizedMatchStart,
            StudentNormalizedLength = result.StudentNormalizedLength,
            ReferenceNormalizedLength = result.ReferenceNormalizedLength,
            IsShortTextCaution = result.IsShortTextCaution,
        };
    }

    /// <summary>参照別の詳細の配列を作る（減点に使う最大値の選択は <see cref="SimilarityDiagnostics.SelectMaximum"/>）。</summary>
    public static ImmutableArray<SimilarityReferenceDiagnostic> FromResults(IEnumerable<SurfaceSimilarityResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        return results.Select((result, index) => From(index + 1, result)).ToImmutableArray();
    }
}
