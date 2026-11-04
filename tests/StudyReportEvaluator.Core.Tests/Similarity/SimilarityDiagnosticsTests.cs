using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using StudyReportEvaluator.Core.Domain;
using StudyReportEvaluator.Core.Scoring;
using StudyReportEvaluator.Core.Serialization;
using StudyReportEvaluator.Core.Similarity;
using StudyReportEvaluator.Core.Tests.Validation;
using Xunit;

namespace StudyReportEvaluator.Core.Tests.Similarity;

// FR-114: 未検証類似度の確認指標化と採点保護（単体テスト）。
public sealed class SimilarityDiagnosticsTests
{
    private readonly SurfaceTextSimilarityCalculator calculator = new();

    // FR-114 AC-010: 原文全文一致と正規化後全文一致を合成値と別の項目で持つ。
    [Fact]
    public void FR114_AC010_raw_and_normalized_exact_match_are_separate_from_composite_score()
    {
        SurfaceSimilarityResult raw = calculator.Calculate("Same answer.", "Same answer.");
        SurfaceSimilarityResult normalized = calculator.Calculate("SAME  answer!", "same answer");
        SurfaceSimilarityResult excerpt = calculator.Calculate(
            "the quick brown fox jumps over the lazy dog",
            "intro the quick brown fox jumps over the lazy dog outro");

        Assert.True(raw.IsRawExactMatch);
        Assert.True(raw.IsNormalizedExactMatch);
        Assert.False(normalized.IsRawExactMatch);
        Assert.True(normalized.IsNormalizedExactMatch);
        Assert.Equal(1m, excerpt.Score);
        Assert.False(excerpt.IsRawExactMatch);
        Assert.False(excerpt.IsNormalizedExactMatch);
    }

    // FR-114 AC-010 / AC-077: 双方の正規化後全文長を分母にした被覆率と、正規化後の 0 起点の一致位置。
    [Fact]
    public void FR114_AC077_coverages_and_normalized_positions_are_reported_for_both_sides()
    {
        SurfaceSimilarityResult result = calculator.Calculate("ab xyz12345", "zzz12345zz");

        // 正規化後: 学生 "abxyz12345"（10）、参照 "zzz12345zz"（10）。最長一致 "z12345"（6）。
        Assert.Equal(6, result.LongestCommonSubstringLength);
        Assert.Equal(4, result.StudentNormalizedMatchStart);
        Assert.Equal(2, result.ReferenceNormalizedMatchStart);
        Assert.Equal(0.6m, result.StudentCoverage);
        Assert.Equal(0.6m, result.ReferenceCoverage);
    }

    // FR-114 AC-077: 一致がなければ位置は -1、被覆率は 0（一致なしを位置 0 と混同しない）。
    [Fact]
    public void FR114_AC077_no_common_character_has_no_position()
    {
        SurfaceSimilarityResult result = calculator.Calculate("abc", "xyz");

        Assert.Equal(0, result.LongestCommonSubstringLength);
        Assert.Equal(-1, result.StudentNormalizedMatchStart);
        Assert.Equal(-1, result.ReferenceNormalizedMatchStart);
        Assert.Equal(0m, result.StudentCoverage);
        Assert.Equal(0m, result.ReferenceCoverage);
    }

    // FR-114 AC-077: 短文注意は PARAM-004（30）以下で付く。境界 30 は注意、31 は注意なし。
    [Theory]
    [InlineData(30, true)]
    [InlineData(31, false)]
    public void FR114_AC077_short_text_caution_uses_param004_boundary(int length, bool expected)
    {
        Assert.Equal(30, SimilarityDiagnostics.ShortTextCautionMaximumLength);
        SurfaceSimilarityResult result = calculator.Calculate(new string('a', length), new string('a', 60));

        Assert.Equal(expected, result.IsShortTextCaution);
        Assert.Equal(expected, SimilarityDiagnostics.IsShortText(length, 60));
    }

    // FR-114 AC-010: 減点に使う代表値は参照別合成値の最大値で、どの参照の値かと結び付く（同点は番号の小さい参照）。
    [Fact]
    public void FR114_AC010_select_maximum_links_value_to_reference_index()
    {
        ImmutableArray<SimilarityReferenceDiagnostic> diagnostics =
        [
            new SimilarityReferenceDiagnostic { ReferenceIndex = 1, Score = 0.2m },
            new SimilarityReferenceDiagnostic { ReferenceIndex = 2, Score = 0.9m },
            new SimilarityReferenceDiagnostic { ReferenceIndex = 3, Score = 0.9m },
            new SimilarityReferenceDiagnostic { ReferenceIndex = 4, Score = 0.5m },
        ];

        SimilarityReferenceDiagnostic? maximum = SimilarityDiagnostics.SelectMaximum(diagnostics);

        Assert.NotNull(maximum);
        Assert.Equal(2, maximum.ReferenceIndex);
        Assert.Equal(0.9m, maximum.Score);
        Assert.Null(SimilarityDiagnostics.SelectMaximum([]));
    }

    // FR-114 AC-010 / AC-077: 参照別の詳細は本文を持たず、計算結果の値をそのまま写す。
    [Fact]
    public void FR114_AC077_reference_diagnostic_copies_values_without_text()
    {
        SurfaceSimilarityResult result = calculator.Calculate("Hi!! Hello, World.", "zzzzhelloworldzzzz");
        SimilarityReferenceDiagnostic diagnostic = SimilarityReferenceDiagnostic.From(1, result);

        Assert.Equal(result.Score, diagnostic.Score);
        Assert.Equal(2, diagnostic.StudentNormalizedMatchStart);
        Assert.Equal(4, diagnostic.ReferenceNormalizedMatchStart);
        Assert.Equal(12, diagnostic.StudentNormalizedLength);
        Assert.Equal(18, diagnostic.ReferenceNormalizedLength);
        Assert.DoesNotContain("hello", JsonSerializer.Serialize(diagnostic), StringComparison.OrdinalIgnoreCase);
        Assert.Throws<ArgumentOutOfRangeException>(() => SimilarityReferenceDiagnostic.From(0, result));
    }

    // FR-114 AC-078: 表示文言は確認指標・未検証であり、判定・確率・断定を含まない。
    [Fact]
    public void FR114_AC078_labels_do_not_claim_determination()
    {
        string described = SimilarityDiagnostics.Describe(
            SimilarityReferenceDiagnostic.From(1, calculator.Calculate("photosynthesis", "photosynthesis")));

        Assert.Contains("確認指標・未検証", described, StringComparison.Ordinal);
        Assert.Contains("短文注意", described, StringComparison.Ordinal);
        foreach (string text in new[] { described, SimilarityDiagnostics.IndicatorNotice })
        {
            Assert.DoesNotMatch(@"(盗用|不正|剽窃)(と判定|と断定|が確定|確定)", text);
            Assert.DoesNotMatch(@"AI使用確率", text);
        }
    }

    // FR-114 AC-011: 係数ゼロなら診断の失敗・欠損でも減点 0。係数が正なら欠損は blank（null）。
    [Fact]
    public void FR114_AC011_zero_weight_isolates_missing_similarity()
    {
        WeightedScoreCalculator scoreCalculator = new();

        Assert.Equal(0m, scoreCalculator.SimilarityPenalty(10m, null, 0m, 1));
        Assert.Equal(0m, scoreCalculator.SimilarityPenalty(10m, 1m, 0m, 1));
        Assert.Null(scoreCalculator.SimilarityPenalty(10m, null, 0.5m, 1));
        Assert.Equal(5m, scoreCalculator.SimilarityPenalty(10m, 1m, 0.5m, 1));
    }

    // FR-114 AC-004: 新規は PARAM-002（0）。旧形式で係数が欠損した定義は旧既定値 0.1 を補い、その事実を通知する。
    [Fact]
    public void FR114_AC004_legacy_definition_without_weight_is_filled_with_notice()
    {
        Assert.Equal(0m, QuantificationDefinition.NewDefaultSimilarityPenaltyWeight);
        string valid = new CanonicalDefinitionSerializer().Serialize(
            C02TestDefinitions.CreateValid() with { SimilarityPenaltyWeight = 0.25m });
        JsonObject node = JsonNode.Parse(valid)!.AsObject();
        Assert.True(node.Remove("similarityPenaltyWeight"));

        CanonicalDefinitionReadResult legacy = new CanonicalDefinitionDeserializer().Deserialize(node.ToJsonString());
        CanonicalDefinitionReadResult explicitRead = new CanonicalDefinitionDeserializer().Deserialize(valid);

        Assert.True(legacy.IsSuccess);
        Assert.Equal(0.1m, legacy.Definition!.SimilarityPenaltyWeight);
        Assert.Contains(legacy.Notices, notice => notice.Contains("0.1", StringComparison.Ordinal));
        Assert.True(explicitRead.IsSuccess);
        Assert.Equal(0.25m, explicitRead.Definition!.SimilarityPenaltyWeight);
        Assert.Empty(explicitRead.Notices);
    }

    // FR-114 AC-004: 壊れた値（文字列）は補完せず読込エラー。
    [Fact]
    public void FR114_AC004_broken_weight_is_an_error_not_a_default()
    {
        string valid = new CanonicalDefinitionSerializer().Serialize(C02TestDefinitions.CreateValid());
        JsonObject node = JsonNode.Parse(valid)!.AsObject();
        node["similarityPenaltyWeight"] = "abc";

        CanonicalDefinitionReadResult read = new CanonicalDefinitionDeserializer().Deserialize(node.ToJsonString());

        Assert.False(read.IsSuccess);
        Assert.NotEmpty(read.Errors);
        Assert.Empty(read.Notices);
    }
}
