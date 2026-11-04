using System.Collections.Immutable;
using StudyReportEvaluator.Core.Similarity;
using Xunit;

namespace StudyReportEvaluator.Core.Tests.Similarity;

// FR-115: 参照集合（PARAM-003 = 5 slot）の記述統計・表示・予定数（単体テスト）。
public sealed class ReferenceSetDiagnosticsTests
{
    private static SimilarityReferenceDiagnostic Diagnostic(int index, decimal score) =>
        new() { ReferenceIndex = index, Score = score };

    // FR-115 AC-009: 新規 run の予定 slot 数は PARAM-003 の 5、旧形式（記録 0）は 1 のまま水増ししない。
    [Fact]
    public void FR115_AC009_AC083_planned_slots_are_five_for_new_runs_and_one_for_legacy()
    {
        Assert.Equal(5, ReferenceSetPolicy.SlotCount);
        Assert.Equal(1, ReferenceSetPolicy.PlannedSlots(0));
        Assert.Equal(1, ReferenceSetPolicy.PlannedSlots(-1));
        Assert.Equal(5, ReferenceSetPolicy.PlannedSlots(5));
    }

    // FR-115 AC-081: 最大・最小・中央値・差は記述統計。採点に使う最大値の選択（FR-114）は変わらない。
    [Fact]
    public void FR115_AC081_statistics_are_descriptive_and_scoring_still_uses_the_maximum_only()
    {
        ImmutableArray<SimilarityReferenceDiagnostic> odd = [Diagnostic(1, 0.2m), Diagnostic(2, 0.8m), Diagnostic(3, 0.5m)];
        SimilarityReferenceStatistics statistics = SimilarityDiagnostics.Summarize(odd)!;
        Assert.Equal(0.8m, statistics.Maximum);
        Assert.Equal(0.2m, statistics.Minimum);
        Assert.Equal(0.5m, statistics.Median);
        Assert.Equal(0.6m, statistics.Range);
        Assert.Equal(2, statistics.MaximumReferenceIndex);
        Assert.Equal(0.8m, SimilarityDiagnostics.SelectMaximum(odd)!.Score);

        ImmutableArray<SimilarityReferenceDiagnostic> even = [Diagnostic(1, 0.2m), Diagnostic(2, 0.4m), Diagnostic(3, 0.6m), Diagnostic(4, 0.9m)];
        Assert.Equal(0.5m, SimilarityDiagnostics.Summarize(even)!.Median);

        // 同点の最大は参照番号の小さいもの。
        ImmutableArray<SimilarityReferenceDiagnostic> tie = [Diagnostic(3, 0.7m), Diagnostic(1, 0.7m), Diagnostic(2, 0.1m)];
        Assert.Equal(1, SimilarityDiagnostics.Summarize(tie)!.MaximumReferenceIndex);

        // 成功参照なしは集計なし（blank）。
        Assert.Null(SimilarityDiagnostics.Summarize([]));
    }

    // FR-115 AC-081: 成功数／予定数・暫定・集計なし・重複の表示。参照本文は含めない。
    [Fact]
    public void FR115_AC081_set_description_shows_success_ratio_provisional_none_and_duplicates()
    {
        ImmutableArray<SimilarityReferenceDiagnostic> all = [Diagnostic(1, 0.2m), Diagnostic(2, 0.8m), Diagnostic(3, 0.5m), Diagnostic(4, 0.5m), Diagnostic(5, 0.1m)];
        string full = SimilarityDiagnostics.DescribeSet(new SimilarityReferenceSetInfo { Planned = 5, Succeeded = 5 }, all);
        Assert.Contains("5/5", full, StringComparison.Ordinal);
        Assert.Contains("最大", full, StringComparison.Ordinal);
        Assert.Contains("最小", full, StringComparison.Ordinal);
        Assert.Contains("中央値", full, StringComparison.Ordinal);
        Assert.DoesNotContain("暫定", full, StringComparison.Ordinal);
        Assert.DoesNotContain("重複", full, StringComparison.Ordinal);

        string provisional = SimilarityDiagnostics.DescribeSet(
            new SimilarityReferenceSetInfo { Planned = 5, Succeeded = 4 },
            all.Take(4));
        Assert.Contains("4/5", provisional, StringComparison.Ordinal);
        Assert.Contains("暫定", provisional, StringComparison.Ordinal);

        string none = SimilarityDiagnostics.DescribeSet(new SimilarityReferenceSetInfo { Planned = 5, Succeeded = 0 }, null);
        Assert.Contains("0/5", none, StringComparison.Ordinal);
        Assert.Contains("集計なし", none, StringComparison.Ordinal);
        Assert.DoesNotContain("最大", none, StringComparison.Ordinal);

        string duplicated = SimilarityDiagnostics.DescribeSet(
            new SimilarityReferenceSetInfo
            {
                Planned = 5,
                Succeeded = 5,
                Duplicates = [new SimilarityDuplicateReference { Slot = 4, DuplicateOfSlot = 3 }],
            },
            all);
        Assert.Contains("重複", duplicated, StringComparison.Ordinal);
    }
}
