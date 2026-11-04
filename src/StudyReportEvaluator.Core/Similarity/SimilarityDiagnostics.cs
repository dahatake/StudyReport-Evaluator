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

    /// <summary>
    /// FR-115 AC-081: 参照集合の記述統計（最大・最小・中央値・差）。表示だけに使い、採点・減点には使わない
    /// （減点は <see cref="SelectMaximum"/> の完全な集合の最大値）。診断が空なら null。
    /// </summary>
    public static SimilarityReferenceStatistics? Summarize(IEnumerable<SimilarityReferenceDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        decimal[] scores = diagnostics.Where(item => item is not null).Select(item => item.Score).Order().ToArray();
        if (scores.Length == 0)
        {
            return null;
        }

        decimal median = scores.Length % 2 == 1
            ? scores[scores.Length / 2]
            : (scores[(scores.Length / 2) - 1] + scores[scores.Length / 2]) / 2m;
        SimilarityReferenceDiagnostic maximum = SelectMaximum(diagnostics)!;
        return new SimilarityReferenceStatistics(
            scores[^1],
            scores[0],
            median,
            scores[^1] - scores[0],
            maximum.ReferenceIndex);
    }

    /// <summary>
    /// FR-115 AC-081 / AC-084: 参照集合の 1 行の集計表示（成功数／予定数・参照別値・記述統計・重複・暫定）。
    /// 一部失敗は暫定集計と明示し、完全な集合の値や減点入力へ読み替えない。成功参照なしは集計なし。学生本文・参照本文は含めない。
    /// </summary>
    public static string DescribeSet(
        SimilarityReferenceSetInfo set,
        IEnumerable<SimilarityReferenceDiagnostic>? diagnostics)
    {
        ArgumentNullException.ThrowIfNull(set);
        List<string> parts =
        [
            string.Create(CultureInfo.InvariantCulture, $"成功数/予定数 {set.Succeeded}/{set.Planned}"),
        ];
        ImmutableArray<SimilarityReferenceDiagnostic> items = diagnostics is null
            ? []
            : diagnostics.Where(item => item is not null).OrderBy(item => item.ReferenceIndex).ToImmutableArray();
        bool partial = set.Succeeded < set.Planned;
        if (partial)
        {
            parts.Add(set.Succeeded == 0
                ? "集計なし（成功した参照なし）"
                : "暫定（一部失敗・成功参照に限った集計で採点・減点には使わない）");
        }

        SimilarityReferenceStatistics? statistics = items.IsEmpty ? null : Summarize(items);
        if (statistics is not null)
        {
            parts.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"最大 {Format(statistics.Maximum)}（参照 {statistics.MaximumReferenceIndex}）"));
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"最小 {Format(statistics.Minimum)}"));
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"中央値 {Format(statistics.Median)}"));
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"最大と最小の差 {Format(statistics.Range)}"));
            parts.Add("参照別 " + string.Join(
                ", ",
                items.Select(item => string.Create(
                    CultureInfo.InvariantCulture,
                    $"{item.ReferenceIndex}:{Format(item.Score)}"))));
        }

        if (!set.Duplicates.IsDefaultOrEmpty)
        {
            parts.Add("重複あり（独立の裏付けとしない）: " + string.Join(
                ", ",
                set.Duplicates.Select(item => string.Create(
                    CultureInfo.InvariantCulture,
                    $"参照 {item.Slot}=参照 {item.DuplicateOfSlot}"))));
        }

        return string.Join(" / ", parts);

        static string Format(decimal value) => value.ToString("0.####", CultureInfo.InvariantCulture);
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
/// FR-115 PARAM-003 と、参照集合の比較条件の版。新規 run の設問ごとの参照回答数は固定の有限集合で、件数設定 UI を持たない。
/// </summary>
public static class ReferenceSetPolicy
{
    /// <summary>PARAM-003: 新規 run の設問ごとの参照回答数。</summary>
    public const int SlotCount = 5;

    /// <summary>旧単独参照 checkpoint の予定数（PARAM-003 へ水増ししない）。</summary>
    public const int LegacySlotCount = 1;

    /// <summary>参照生成の比較入力（質問文字列のみ）と結果収集のための system 指示の版。</summary>
    public const string SystemTemplateVersion = "reference-answer-v1";

    /// <summary>アプリの比較方式の版（FR-114 の表面類似度）。</summary>
    public const string ComparisonVersion = "surface-similarity-v1";

    /// <summary>比較前の正規化条件の版。</summary>
    public const string NormalizationVersion = "surface-normalization-v1";

    /// <summary>SDK で観測できない・旧形式で欠けている条件の表示。</summary>
    public const string Unobserved = "未取得";

    /// <summary>checkpoint の予定数欄（0 は旧形式）から、設問ごとの予定 slot 数を得る。</summary>
    public static int PlannedSlots(int recordedSlotCount) =>
        recordedSlotCount <= 0 ? LegacySlotCount : recordedSlotCount;
}

/// <summary>FR-115 AC-081: 参照集合の記述統計（表示のみ。採点・減点には使わない）。</summary>
public sealed record SimilarityReferenceStatistics(
    decimal Maximum,
    decimal Minimum,
    decimal Median,
    decimal Range,
    int MaximumReferenceIndex);

/// <summary>FR-115 AC-081: 同一本文の参照（<see cref="Slot"/> が <see cref="DuplicateOfSlot"/> と同じ本文）。</summary>
public sealed record SimilarityDuplicateReference
{
    public int Slot { get; init; }

    public int DuplicateOfSlot { get; init; }
}

/// <summary>
/// FR-115 AC-081: ある設問・学生の比較に使った参照集合の成功数／予定数と重複。参照本文は持たない。
/// </summary>
public sealed record SimilarityReferenceSetInfo
{
    public int Planned { get; init; }

    public int Succeeded { get; init; }

    public ImmutableArray<SimilarityDuplicateReference> Duplicates { get; init; } = [];
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
