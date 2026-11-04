using System.Collections;
using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using StudyReportEvaluator.App.Copilot;
using StudyReportEvaluator.App.ViewModels;
using StudyReportEvaluator.App.Workbooks.Checkpoint;
using StudyReportEvaluator.App.Workbooks.Intake;
using StudyReportEvaluator.App.Workbooks.Reading;
using StudyReportEvaluator.App.Workbooks.Writing;
using StudyReportEvaluator.App.Workflow;
using StudyReportEvaluator.Core.Domain;
using StudyReportEvaluator.Core.Prompting;
using StudyReportEvaluator.Core.Serialization;
using StudyReportEvaluator.Core.Similarity;
using StudyReportEvaluator.Core.Validation;
using Xunit;

namespace StudyReportEvaluator.SystemTests.Integration;

/// <summary>
/// FR-114（AC-004, AC-010, AC-011, AC-012, AC-077, AC-078, AC-079）の System Test。
/// 期待値は要求定義書の受入基準（§7.5・§8 と PARAM-002=0、PARAM-004=30）だけから決める。
/// 実装は、合成 run（fake AI・合成 workbook）の起動・画面モデルへの到達・完成 workbook を読むためにだけ使う。
/// 実 AI・実 credential・外部書込は使わない。
///
/// テストが前提にする観測点（新規の診断項目。名前はこのファイルが定める契約）:
///   SurfaceTextSimilarityCalculator.Calculate(student, reference) が返す結果に、次の公開プロパティを持つ。
///     IsRawExactMatch (bool)             原文全文一致
///     IsNormalizedExactMatch (bool)      正規化後全文一致
///     LongestCommonSubstringLength (int) 最長連続部分一致の文字数（正規化後）
///     StudentCoverage (decimal)          最長一致長 ÷ 学生側の正規化後全文長（4桁丸め）
///     ReferenceCoverage (decimal)        最長一致長 ÷ 参照側の正規化後全文長（4桁丸め）
///     StudentNormalizedMatchStart (int)   最長一致の学生側の開始位置（正規化後文字の 0 起点）
///     ReferenceNormalizedMatchStart (int) 最長一致の参照側の開始位置（正規化後文字の 0 起点）
///     IsShortTextCaution (bool)          いずれかの正規化後全文長が PARAM-004 以下
///   読込結果（CanonicalDefinitionReadResult）は、旧形式で係数を補ったときに、その事実を公開の文字列（または文字列を含む要素）で示す。
///   通常評価と別の確認指標であることを、完成 workbook と結果画面に「確認指標・未検証」と表示する。
/// </summary>
public sealed partial class SimilarityIndicatorTests
{
    private const string ResultsSheet = "Quantification_Results";
    private const string ConfigSheet = "Quantification_Config";
    private const string ScoreSheet = "採点結果";
    private const string IndicatorLabel = "確認指標・未検証";
    private const decimal Param002NewDefaultWeight = 0m; // PARAM-002
    private const int Param004ShortTextLimit = 30;       // PARAM-004
    private const decimal LegacyDefaultWeight = 0.1m;    // AC-004: 旧形式で欠損した係数の補完値

    private const string ReferenceText = "answer reference text alpha beta gamma delta";
    private const string IdenticalAnswer = ReferenceText;
    private const string DifferentAnswer = "answer unrelated student wording zeta";
    private const string FailMarker = "FAIL-NORMAL";
    private const string FailingAnswer = "answer " + FailMarker + " marker";
    private const string StudentBodyCanary = "STUDENTBODYCANARY";

    // 行: 2=参照と同一, 3=別の文, 4=空回答, 5=通常評価の技術的失敗。
    private static readonly string[] Answers = [IdenticalAnswer, DifferentAnswer + " " + StudentBodyCanary, string.Empty, FailingAnswer];

    [GeneratedRegex(@"^\$?([A-Z]+)\$?(\d+)$")]
    private static partial Regex CellRef();

    // ------------------------------------------------------------------
    // FR-114 AC-004: 新規だけ PARAM-002、明示係数（ゼロを含む）はそのまま往復、旧形式の欠損は 0.1 を補い事実を表示、
    // 壊れた・範囲外の値は検証エラー（新既定値へ黙って置換しない）。
    // ------------------------------------------------------------------
    [Fact]
    public async Task FR114_AC004_similarity_weight_new_default_explicit_roundtrip_legacy_fill_and_invalid()
    {
        // 新規定義は PARAM-002。
        QuantificationDesignViewModel fresh = new();
        Assert.Equal(Param002NewDefaultWeight, fresh.SimilarityPenaltyWeight);
        Assert.Equal(Param002NewDefaultWeight, fresh.Draft.SimilarityPenaltyWeight);

        // 既存定義の明示係数（ゼロ・非ゼロ）は画面モデルに読み込んでもそのまま。
        foreach (decimal explicitWeight in new[] { 0m, 0.25m, 0.1m })
        {
            QuantificationDefinition seed = Definition(explicitWeight);
            QuantificationDesignViewModel loaded = new(seed);
            Assert.Equal(explicitWeight, loaded.SimilarityPenaltyWeight);

            // 保存（canonical JSON）→読戻しの往復。
            string json = new CanonicalDefinitionSerializer().Serialize(loaded.Draft);
            CanonicalDefinitionReadResult back = new CanonicalDefinitionDeserializer().Deserialize(json);
            Assert.True(back.IsSuccess, string.Join(";", back.Errors.Select(e => e.Kind + ":" + e.Message)));
            Assert.Equal(explicitWeight, back.Definition!.SimilarityPenaltyWeight);
            // 配点の明示値は従来どおり往復する。
            Assert.Equal(seed.BasePoints, back.Definition.BasePoints);
            Assert.Equal(seed.Questions[0].Points, back.Definition.Questions[0].Points);
        }

        // 明示係数ゼロの run の Config 読戻しはゼロのまま（再起動後の読込に相当）。
        using TempDir dir = new();
        string final0 = await RunAsync(dir, weight: 0m, failReferences: false, label: "cfg0");
        using (SpreadsheetDocument doc = SpreadsheetDocument.Open(final0, false))
        {
            CanonicalDefinitionReadResult cfg = new ConfigSheetReader().Read(doc.WorkbookPart!, ConfigSheet);
            Assert.True(cfg.IsSuccess, string.Join(";", cfg.Errors.Select(e => e.Kind + ":" + e.Message)));
            Assert.Equal(0m, cfg.Definition!.SimilarityPenaltyWeight);
        }

        // 旧形式: 係数が欠損した定義は 0.1 を補い、その事実を示す（新既定値 0 にしない）。
        string valid = new CanonicalDefinitionSerializer().Serialize(Definition(0.25m));
        string legacy = RemoveTopLevelProperty(valid, "similarityPenaltyWeight");
        Assert.DoesNotContain("similarityPenaltyWeight", legacy, StringComparison.Ordinal);
        CanonicalDefinitionReadResult legacyRead = new CanonicalDefinitionDeserializer().Deserialize(legacy);
        Assert.True(legacyRead.IsSuccess, "旧形式（係数欠損）は読込できなければならない: " + string.Join(";", legacyRead.Errors.Select(e => e.Kind + ":" + e.Message)));
        Assert.Equal(LegacyDefaultWeight, legacyRead.Definition!.SimilarityPenaltyWeight);
        List<string> notices = CollectStrings(legacyRead, depth: 3)
            .Where(s => s.Contains("similarityPenaltyWeight", StringComparison.OrdinalIgnoreCase)
                     || s.Contains("類似度減点係数", StringComparison.Ordinal)
                     || s.Contains("減点係数", StringComparison.Ordinal))
            .ToList();
        Assert.True(notices.Count > 0, "旧形式で係数 0.1 を補った事実が読込結果に示されていない");
        Assert.Contains(notices, s => s.Contains("0.1", StringComparison.Ordinal));
        // 補完の通知があっても、明示係数のある定義には通知を出さない。
        CanonicalDefinitionReadResult explicitRead = new CanonicalDefinitionDeserializer().Deserialize(valid);
        Assert.DoesNotContain(
            CollectStrings(explicitRead, depth: 3),
            s => s.Contains("0.1", StringComparison.Ordinal) && (s.Contains("減点係数", StringComparison.Ordinal) || s.Contains("similarityPenaltyWeight", StringComparison.OrdinalIgnoreCase)));

        // 異常系: 壊れた値（文字列）・範囲外（1.5、-0.1）は検証エラー。新既定値（0）へ黙って置換しない。
        foreach (string broken in new[] { "\"abc\"", "1.5", "-0.1" })
        {
            string json = ReplaceTopLevelNumber(valid, "similarityPenaltyWeight", broken);
            CanonicalDefinitionReadResult read = new CanonicalDefinitionDeserializer().Deserialize(json);
            if (read.IsSuccess)
            {
                Assert.NotEqual(Param002NewDefaultWeight, read.Definition!.SimilarityPenaltyWeight);
                DefinitionValidationResult validation = new QuantificationDefinitionValidator().Validate(read.Definition);
                Assert.False(validation.IsValid, "範囲外の係数 " + broken + " が検証を通った");
                Assert.Contains(validation.Errors, e => e.Field.Contains("SimilarityPenaltyWeight", StringComparison.Ordinal));
            }
            else
            {
                Assert.NotEmpty(read.Errors);
            }
        }
    }

    // ------------------------------------------------------------------
    // FR-114 AC-011 AC-012: 係数ゼロなら診断の失敗・欠損でも減点ゼロで総合点は通常評価から。係数が正なら
    // 完全な診断だけ Points×Similarity×Weight、不完全なら減点と総合点は blank。通常評価の失敗による blank は係数ゼロでも残す。
    // 空回答は AI へ送らずゼロ。技術的失敗をゼロや一致なしにしない。Config 参照の数式と UI の cached preview・採点結果シートが一致。
    // ------------------------------------------------------------------
    [Fact]
    public async Task FR114_AC011_AC012_weight_zero_isolates_diagnostic_failure_and_positive_weight_propagates_blank()
    {
        using TempDir dir = new();
        const decimal basePoints = 90m;
        const decimal earned = 7m; // P=10, raw 7/10 → R=0.7 → QuestionEarned=7（§8.1）

        // A: W=0・診断正常
        RunOutcome a = await RunWithOutcomeAsync(dir, 0m, failReferences: false, "A");
        Assert.Equal(0, a.NormalCallsForEmpty); // 空回答は AI へ送らない（AC-012）
        AssertRow(a, 2, penalty: 0m, finalScore: basePoints + earned);
        AssertRow(a, 4, penalty: 0m, finalScore: basePoints, questionEarned: 0m);
        // 通常評価の技術的失敗: 対象値 blank・失敗 status、係数ゼロでも総合点 blank（AC-011・AC-012）
        AssertNormalFailureRow(a, 5);

        // B: W=0・診断（参照生成）失敗 → 診断値は blank と失敗 status。減点 0、総合点は通常評価から。
        RunOutcome b = await RunWithOutcomeAsync(dir, 0m, failReferences: true, "B");
        foreach (int row in new[] { 2, 3 })
        {
            Assert.True(string.IsNullOrEmpty(b.Cell(row, "Q1.Similarity_AI_Raw")), "診断失敗を一致なし（0）に変換してはならない");
            Assert.NotEqual(ResultsStatusCodes.Success, b.Cell(row, "Q1.Similarity_Status"));
            Assert.False(string.IsNullOrEmpty(b.Cell(row, "Q1.Similarity_Status")));
        }

        AssertRow(b, 2, penalty: 0m, finalScore: basePoints + earned);
        AssertNormalFailureRow(b, 5);

        // C: W=0.5・完全な診断 → 減点 = Points × Similarity × Weight。参照と同一の回答は Similarity=1（§7.5）。
        RunOutcome c = await RunWithOutcomeAsync(dir, 0.5m, failReferences: false, "C");
        decimal similarity = ParseDec(c.Cell(2, "Q1.Similarity_AI_Raw"));
        Assert.Equal(1m, similarity);
        AssertRow(c, 2, penalty: 10m * similarity * 0.5m, finalScore: basePoints + earned - (10m * similarity * 0.5m));
        decimal sim3 = ParseDec(c.Cell(3, "Q1.Similarity_AI_Raw"));
        AssertRow(c, 3, penalty: Math.Round(10m * sim3 * 0.5m, 4), finalScore: null, tolerance: 0.1m);

        // D: W=0.5・診断失敗 → 減点・総合点 blank（採点結果シートの合計も同じ条件）。
        RunOutcome d = await RunWithOutcomeAsync(dir, 0.5m, failReferences: true, "D");
        Assert.True(string.IsNullOrEmpty(d.Cell(2, "Q1.Similarity_Penalty")), "W>0 で診断不完全なら減点は blank");
        Assert.True(string.IsNullOrEmpty(d.Cell(2, "Final_Raw")));
        Assert.True(string.IsNullOrEmpty(d.Cell(2, "Final_Score")));
        Assert.Null(d.UiFinalScore(2));

        // どの run でも、減点は Config の係数セルを参照する数式で、学生間最大類似度（情報列）を使わない（AC-078 の一部）。
        foreach (RunOutcome o in new[] { a, b, c, d })
        {
            string formula = o.Formula(2, "Q1.Similarity_Penalty");
            Assert.Contains(ConfigSheet, formula, StringComparison.Ordinal);
            Assert.DoesNotContain("0.5", formula, StringComparison.Ordinal);
            string peerColumn = o.Column("Q1.Similarity_Peer_Max");
            Assert.DoesNotMatch(new Regex(@"(?<![A-Z])\$?" + peerColumn + @"\$?\d"), formula);
            Assert.False(string.IsNullOrEmpty(o.Formula(2, "Final_Score")), "FinalScore は数式セル");
        }
    }

    // ------------------------------------------------------------------
    // FR-114 AC-010 AC-077: ローカル計算で、全文一致（原文・正規化後）、最長連続部分一致の文字数、双方の被覆率、
    // 一致箇所（正規化後の位置）、短文注意（PARAM-004 以下）を別項目で確認でき、合成値が最大でも全文一致とは表示しない。
    // ------------------------------------------------------------------
    [Fact]
    public async Task FR114_AC010_AC077_local_diagnostic_separates_exact_partial_coverage_positions_and_short_text_caution()
    {
        SurfaceTextSimilarityCalculator calculator = new();

        // 全文一致（原文も正規化後も一致）
        object exact = calculator.Calculate("answer text 123", "answer text 123");
        Assert.True(Prop<bool>(exact, "IsRawExactMatch"));
        Assert.True(Prop<bool>(exact, "IsNormalizedExactMatch"));
        Assert.Equal(1m, Prop<decimal>(exact, "StudentCoverage"));
        Assert.Equal(1m, Prop<decimal>(exact, "ReferenceCoverage"));

        // 表記差: 原文は不一致、正規化後は一致（NFKC・小文字化・空白・句読点・記号除去）
        object notation = calculator.Calculate("Answer,  Text １２３!", "answer text 123");
        Assert.False(Prop<bool>(notation, "IsRawExactMatch"));
        Assert.True(Prop<bool>(notation, "IsNormalizedExactMatch"));

        // 抜粋: 学生回答が長い参照の一部。合成値は最大（学生側被覆 1）でも全文一致ではない。
        string longReference = "zzzz the quick brown fox jumps over the lazy dog near the river bank yyyy";
        object excerpt = calculator.Calculate("the quick brown fox jumps over the lazy dog", longReference);
        Assert.Equal(1m, ((SurfaceSimilarityResult)excerpt).Score);
        Assert.False(Prop<bool>(excerpt, "IsNormalizedExactMatch"));
        Assert.False(Prop<bool>(excerpt, "IsRawExactMatch"));
        int excerptLcs = Prop<int>(excerpt, "LongestCommonSubstringLength");
        Assert.Equal("thequickbrownfoxjumpsoverthelazydog".Length, excerptLcs);
        Assert.Equal(1m, Prop<decimal>(excerpt, "StudentCoverage"));
        int refLen = "zzzzthequickbrownfoxjumpsoverthelazydogneartheriverbankyyyy".Length;
        Assert.Equal(Math.Round((decimal)excerptLcs / refLen, 4), Prop<decimal>(excerpt, "ReferenceCoverage"));
        Assert.Equal(0, Prop<int>(excerpt, "StudentNormalizedMatchStart"));
        Assert.Equal(4, Prop<int>(excerpt, "ReferenceNormalizedMatchStart"));
        Assert.False(Prop<bool>(excerpt, "IsShortTextCaution")); // 双方 30 文字超

        // 一致位置: 原文位置（"Hello" は原文 5 文字目）と正規化後の位置（2）を混同しない。
        object position = calculator.Calculate("Hi!! Hello, World.", "zzzzhelloworldzzzz");
        Assert.Equal(10, Prop<int>(position, "LongestCommonSubstringLength"));
        Assert.Equal(2, Prop<int>(position, "StudentNormalizedMatchStart"));
        Assert.Equal(4, Prop<int>(position, "ReferenceNormalizedMatchStart"));
        Assert.Equal(Math.Round(10m / 12m, 4), Prop<decimal>(position, "StudentCoverage"));
        Assert.Equal(Math.Round(10m / 18m, 4), Prop<decimal>(position, "ReferenceCoverage"));

        // 短文注意: いずれかの正規化後全文長が PARAM-004 以下なら注意（境界 30 / 31）。
        string thirty = new('a', Param004ShortTextLimit);
        string thirtyOne = new('b', Param004ShortTextLimit + 1);
        string longer = new('c', 50);
        Assert.True(Prop<bool>(calculator.Calculate(thirty, longer), "IsShortTextCaution"));
        Assert.True(Prop<bool>(calculator.Calculate(longer, thirty), "IsShortTextCaution"));
        Assert.False(Prop<bool>(calculator.Calculate(thirtyOne, longer + "b"), "IsShortTextCaution"));
        // 同じ短い正答: 全文一致でも短文注意が付く（注意値は判定・減点閾値ではない）。
        object shortSame = calculator.Calculate("photosynthesis", "photosynthesis");
        Assert.True(Prop<bool>(shortSame, "IsNormalizedExactMatch"));
        Assert.True(Prop<bool>(shortSame, "IsShortTextCaution"));

        // 編集（一部書換え）: 部分一致は全文一致ではなく、双方の被覆率は 1 未満。
        object edited = calculator.Calculate(
            "the quick brown fox leaps over the sleepy dog by the river",
            "the quick brown fox jumps over the lazy dog by the river");
        Assert.False(Prop<bool>(edited, "IsNormalizedExactMatch"));
        Assert.True(Prop<decimal>(edited, "StudentCoverage") < 1m);
        Assert.True(Prop<decimal>(edited, "ReferenceCoverage") < 1m);
        Assert.Equal("thequickbrownfox".Length, Prop<int>(edited, "LongestCommonSubstringLength"));

        // 従来の合成指標（§7.5）は保持される。
        SurfaceSimilarityResult composite = calculator.Calculate("Hi!! Hello, World.", "zzzzhelloworldzzzz");
        Assert.InRange(composite.Score, 0m, 1m);
        Assert.Equal(12, composite.StudentNormalizedLength);
        Assert.Equal(18, composite.ReferenceNormalizedLength);

        // 位置情報だけを新規診断へ保存し、学生本文を診断の列へ複写しない（AC-077）。
        using TempDir dir = new();
        RunOutcome run = await RunWithOutcomeAsync(dir, 0m, failReferences: false, "pos");
        foreach ((string header, string column) in run.Headers.Where(h => h.Key.StartsWith("Q1.Similarity", StringComparison.Ordinal)).Select(h => (h.Key, h.Value)))
        {
            Assert.DoesNotContain(StudentBodyCanary, run.Cell(3, header) ?? string.Empty, StringComparison.Ordinal);
            _ = column;
        }
    }

    // ------------------------------------------------------------------
    // FR-114 AC-078 AC-079: 算出済み・失敗の診断を通常評価と別に「確認指標・未検証」と表示し、履歴・確率・不正の断定をしない。
    // 旧結果 workbook（新診断欄なし）は読込で係数・合成値・採点値を保持し、開くだけで上書きしない。
    // ------------------------------------------------------------------
    [Fact]
    public async Task FR114_AC078_AC079_indicator_is_labelled_unverified_and_legacy_results_are_preserved()
    {
        using TempDir dir = new();
        foreach (bool failReferences in new[] { false, true })
        {
            RunOutcome run = await RunWithOutcomeAsync(dir, 0m, failReferences, failReferences ? "lbl-fail" : "lbl-ok");
            List<string> texts = AllCellTexts(run.FinalPath);
            Assert.Contains(texts, t => t.Contains(IndicatorLabel, StringComparison.Ordinal));
            AssertNoDeterminationClaims(texts);

            // 結果詳細（画面モデル）にも同じ区別を表示する。
            ResultsOutputViewModel results = run.Results;
            ResultsRowScoreViewModel first = results.RowScores.First();
            TrySelect(results, first);
            List<string> uiTexts = CollectStrings(results, depth: 3);
            Assert.Contains(uiTexts, t => t.Contains(IndicatorLabel, StringComparison.Ordinal));
            AssertNoDeterminationClaims(uiTexts);
        }

        // 旧結果 workbook（新診断欄がない v4.6 以前の形式）。読込で旧の係数・合成値・採点値を保持し、上書きしない。
        string fixture = Path.Combine(RepoRoot(), "tests", "system", "integration", "Fixtures", "fr114-legacy-result.xlsx");
        Assert.True(File.Exists(fixture), "旧形式 fixture がない: " + fixture);
        string copy = Path.Combine(dir.Path, "legacy.xlsx");
        File.Copy(fixture, copy);
        string before = Sha(copy);
        using (SpreadsheetDocument doc = SpreadsheetDocument.Open(copy, false))
        {
            CanonicalDefinitionReadResult cfg = new ConfigSheetReader().Read(doc.WorkbookPart!, ConfigSheet);
            Assert.True(cfg.IsSuccess, string.Join(";", cfg.Errors.Select(e => e.Kind + ":" + e.Message)));
            Assert.Equal(LegacyDefaultWeight, cfg.Definition!.SimilarityPenaltyWeight); // 旧の明示係数を新既定値へ置換しない
            Dictionary<string, string> headers = HeaderMap(doc, Sheet(doc, ResultsSheet));
            Assert.DoesNotContain(headers.Keys, h => h.Contains("Coverage", StringComparison.OrdinalIgnoreCase)); // fixture は旧形式
            string? legacySim = CellByHeader(doc, ResultsSheet, headers, 2, "Q1.Similarity_AI_Raw");
            Assert.Equal(1m, ParseDec(legacySim));
        }

        Assert.Equal(before, Sha(copy)); // 開くだけで再生成・再採点・上書きしない
        Assert.Equal(Sha(fixture), before);
    }

    // ================= helpers =================

    private sealed class RunOutcome
    {
        internal required string FinalPath { get; init; }
        internal required Dictionary<string, string> Headers { get; init; } // header -> column
        internal required Dictionary<int, Dictionary<string, (string? Value, string? Formula)>> Rows { get; init; }
        internal required Dictionary<int, (string? Value, string? Formula)> ScoreTotals { get; init; }
        internal required ResultsOutputViewModel Results { get; init; }
        internal int NormalCallsForEmpty { get; init; }

        internal string Column(string header) => Headers.TryGetValue(header, out string? c) ? c : throw new Xunit.Sdk.XunitException("列がない: " + header);

        internal string? Cell(int sourceRow, string header)
        {
            _ = Column(header);
            return Rows[sourceRow].TryGetValue(header, out var v) ? v.Value : null;
        }

        internal string Formula(int sourceRow, string header)
        {
            _ = Column(header);
            return Rows[sourceRow].TryGetValue(header, out var v) ? v.Formula ?? string.Empty : string.Empty;
        }

        internal decimal? UiFinalScore(int sourceRow) => Results.RowScores.Single(r => r.SourceRowNumber == sourceRow).FinalScore;
    }

    private static void AssertRow(RunOutcome o, int row, decimal penalty, decimal? finalScore, decimal? questionEarned = null, decimal tolerance = 0.0001m)
    {
        Assert.InRange(ParseDec(o.Cell(row, "Q1.Similarity_Penalty")), penalty - tolerance, penalty + tolerance);
        if (questionEarned is not null)
        {
            Assert.Equal(questionEarned.Value, ParseDec(o.Cell(row, "Q1.Question_Earned")));
        }

        decimal raw = ParseDec(o.Cell(row, "Final_Raw"));
        decimal score = ParseDec(o.Cell(row, "Final_Score"));
        if (finalScore is not null)
        {
            Assert.InRange(score, finalScore.Value - tolerance, finalScore.Value + tolerance);
        }

        Assert.Equal(Math.Clamp(raw, 0m, 100m), score);
        // UI の cached preview と一致
        Assert.Equal(score, o.UiFinalScore(row));
        // `採点結果` の合計も同じ条件（cached があれば一致、blank なら blank）。
        if (o.ScoreTotals.TryGetValue(row, out var total) && !string.IsNullOrEmpty(total.Value))
        {
            Assert.Equal(score, ParseDec(total.Value));
        }
    }

    private static void AssertNormalFailureRow(RunOutcome o, int row)
    {
        string? normalized = o.Cell(row, "Q1.Question_Normalized");
        Assert.True(string.IsNullOrEmpty(normalized), "技術的失敗を 0 に変換してはならない: " + normalized);
        string? status = o.Rows[row].Where(p => p.Key.StartsWith("Q1.", StringComparison.Ordinal) && p.Key.EndsWith(".Status", StringComparison.Ordinal) && !p.Key.Contains("Similarity", StringComparison.Ordinal))
            .Select(p => p.Value.Value).FirstOrDefault();
        Assert.Equal(ResultsStatusCodes.AiTimeout, status);
        Assert.True(string.IsNullOrEmpty(o.Cell(row, "Final_Raw")));
        Assert.True(string.IsNullOrEmpty(o.Cell(row, "Final_Score")));
        Assert.Null(o.UiFinalScore(row));
        if (o.ScoreTotals.TryGetValue(row, out var total))
        {
            Assert.True(string.IsNullOrEmpty(total.Value), "採点結果の合計も blank");
        }
    }

    private static void AssertNoDeterminationClaims(IEnumerable<string> texts)
    {
        Regex[] claims =
        [
            new(@"AI使用確率\s*[:：=]?\s*\d"),
            new(@"AI(を)?使用(した|している)(と判定|と断定|ことが確定)"),
            new(@"(盗用|不正|剽窃)(と判定|と断定|が確定|確定)"),
            new(@"自力(と判定|と断定|が確定|を保証)"),
            new(@"(?i)probability of ai use\s*[:=]?\s*\d"),
        ];
        foreach (string text in texts)
        {
            foreach (Regex claim in claims)
            {
                Assert.False(claim.IsMatch(text), "断定表示: " + text);
            }
        }
    }

    private static async Task<string> RunAsync(TempDir dir, decimal weight, bool failReferences, string label) =>
        (await RunWithOutcomeAsync(dir, weight, failReferences, label)).FinalPath;

    private static async Task<RunOutcome> RunWithOutcomeAsync(TempDir dir, decimal weight, bool failReferences, string label)
    {
        string root = Path.Combine(dir.Path, label);
        Directory.CreateDirectory(root);
        string input = Path.Combine(root, "input.xlsx");
        string output = Path.Combine(root, "out");
        Directory.CreateDirectory(output);
        CreateInput(input);
        NormalRunner normal = new();
        WorkbookMetadata metadata = new WorkbookMetadataReader().Read(input);
        DurableQuantificationOrchestrator orchestrator = new(
            new OpenXmlEvaluationRowSource(input),
            normal,
            new ReferenceRunner(failReferences),
            new SpecialRunner());
        RunSummary summary = await orchestrator.RunAsync(
            new DurableQuantificationRunRequest
            {
                Run = new QuantificationRunRequest
                {
                    DraftDefinition = Definition(weight),
                    WorkbookMetadata = metadata,
                    InputPath = input,
                    ModelId = "model-test",
                    MaximumPromptTokens = 64_000,
                    MaximumContextWindowTokens = 128_000,
                    MaxConcurrency = 1,
                    UseDurableWorkflow = true,
                },
                Runtime = new CheckpointRuntimeIdentity
                {
                    ApplicationIdentity = "StudyReportEvaluator.App/4.0.0",
                    CliVersion = "1.0.79",
                    CliSha256 = new string('A', 64),
                    SdkInformationalVersion = "1.0.11",
                },
                OutputDirectory = output,
            },
            cancellationToken: TestContext.Current.CancellationToken);
        string finalPath = Assert.IsType<string>(summary.FinalPath);
        Assert.True(File.Exists(finalPath), "完成 workbook が出力されない（診断だけの失敗が出力を妨げた）");

        Dictionary<string, string> headers;
        Dictionary<int, Dictionary<string, (string?, string?)>> rows = [];
        Dictionary<int, (string?, string?)> totals = [];
        using (SpreadsheetDocument doc = SpreadsheetDocument.Open(finalPath, false))
        {
            WorksheetPart results = Sheet(doc, ResultsSheet);
            headers = HeaderMap(doc, results);
            string sourceColumn = headers["SourceRow"];
            foreach (Row r in results.Worksheet!.Descendants<Row>().Where(r => r.RowIndex!.Value > 1))
            {
                Dictionary<string, (string?, string?)> cells = [];
                string? source = null;
                foreach (Cell cell in r.Elements<Cell>())
                {
                    string col = CellRef().Match(cell.CellReference!.Value!).Groups[1].Value;
                    string? text = CellText(doc, cell);
                    if (col == sourceColumn)
                    {
                        source = text;
                    }

                    string? header = headers.FirstOrDefault(p => p.Value == col).Key;
                    if (header is not null)
                    {
                        cells[header] = (text, cell.CellFormula?.Text);
                    }
                }

                if (int.TryParse(source, NumberStyles.Integer, CultureInfo.InvariantCulture, out int sourceRow))
                {
                    rows[sourceRow] = cells;
                }
            }

            Sheet? scoreSheet = doc.WorkbookPart!.Workbook!.Sheets!.Elements<Sheet>().FirstOrDefault(s => s.Name == ScoreSheet);
            if (scoreSheet is not null)
            {
                WorksheetPart score = (WorksheetPart)doc.WorkbookPart.GetPartById(scoreSheet.Id!.Value!);
                Dictionary<string, string> scoreHeaders = HeaderMap(doc, score);
                if (scoreHeaders.TryGetValue("ID", out string? idCol) && scoreHeaders.TryGetValue("合計", out string? totalCol))
                {
                    foreach (Row r in score.Worksheet!.Descendants<Row>().Where(r => r.RowIndex!.Value > 1))
                    {
                        int index = (int)r.RowIndex!.Value;
                        Cell? idCell = r.Elements<Cell>().FirstOrDefault(c => c.CellReference == idCol + index);
                        Cell? totalCell = r.Elements<Cell>().FirstOrDefault(c => c.CellReference == totalCol + index);
                        string? id = idCell is null ? null : CellText(doc, idCell);
                        int key = int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : index;
                        if (totalCell is not null)
                        {
                            totals[key] = (CellText(doc, totalCell), totalCell.CellFormula?.Text);
                        }
                    }
                }
            }
        }

        ResultsOutputViewModel vm = new(
            new ResultsOutputBoundary(),
            new ExecutionRunContext(
                summary,
                input,
                "model-test",
                new CopilotRuntimeIdentity(Path.Combine(Path.GetTempPath(), "copilot-st-test.exe"), "1.0.82", new string('B', 64), "1.0.11")));

        return new RunOutcome
        {
            FinalPath = finalPath,
            Headers = headers,
            Rows = rows,
            ScoreTotals = totals,
            Results = vm,
            NormalCallsForEmpty = normal.EmptyCalls,
        };
    }

    private static QuantificationDefinition Definition(decimal weight) =>
        new()
        {
            Id = "DEF-ST114",
            Name = "FR-114 system-test definition",
            Revision = "1",
            SourceSheet = "Original",
            HeaderRow = 1,
            FirstDataRow = 2,
            LastDataRow = 1 + Answers.Length,
            BasePoints = 90m,
            SpecialPoints = 0m,
            SimilarityPenaltyWeight = weight,
            RoundingDigits = 1,
            Questions =
            [
                new QuestionDefinition
                {
                    Id = "Q1",
                    DisplayName = "Question Q1",
                    QuestionText = "Explain the reference topic.",
                    PrimarySourceColumn = "A",
                    SupportingSourceColumns = [],
                    Points = 10m,
                    Enabled = true,
                    Evaluators =
                    [
                        new EvaluatorDefinition
                        {
                            Id = "K1",
                            DisplayName = "Evaluator K1",
                            Type = EvaluatorType.KnowledgeCoverage,
                            Weight = 1m,
                            Range = new ScoreRange(0m, 10m),
                            BuiltInTemplateVersion = BuiltInPromptTemplates.KnowledgeTemplateVersion,
                            Enabled = true,
                            Criteria =
                            [
                                new CriterionDefinition { Id = "K1-C1", DisplayName = "Criterion K1", Description = "Description K1", Weight = 1m, Enabled = true },
                            ],
                        },
                    ],
                },
            ],
        };

    private static void CreateInput(string path)
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create(path, DocumentFormat.OpenXml.SpreadsheetDocumentType.Workbook);
        WorkbookPart workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new Workbook();
        Sheets sheets = workbookPart.Workbook.AppendChild(new Sheets());
        WorksheetPart part = workbookPart.AddNewPart<WorksheetPart>();
        SheetData data = new(new Row(Inline("A1", "Primary A")) { RowIndex = 1 });
        for (int i = 0; i < Answers.Length; i++)
        {
            uint index = (uint)(i + 2);
            data.Append(Answers[i].Length == 0 ? new Row { RowIndex = index } : new Row(Inline("A" + index, Answers[i])) { RowIndex = index });
        }

        part.Worksheet = new Worksheet(data);
        sheets.Append(new Sheet { Id = workbookPart.GetIdOfPart(part), SheetId = 1, Name = "Original" });
        workbookPart.Workbook.Save();
    }

    private sealed class NormalRunner : IEvaluationRunner
    {
        internal int EmptyCalls { get; private set; }

        public Task<EvaluationRunnerResult> EvaluateAsync(SafeEvaluationPayload payload, string modelId, CancellationToken cancellationToken)
        {
            string answer = payload.PrimarySource.Value ?? string.Empty;
            if (answer.Trim().Length == 0)
            {
                EmptyCalls++;
            }

            if (answer.Contains(FailMarker, StringComparison.Ordinal))
            {
                return Task.FromResult(EvaluationRunnerResult.Failed(ResultsStatusCodes.AiTimeout, attemptCount: 3));
            }

            return Task.FromResult(EvaluationRunnerResult.Succeeded(
                new QuantificationResult
                {
                    EvaluatorId = payload.EvaluatorId,
                    Criteria = payload.ExpectedCriteria
                        .Select(criterion => new CriterionQuantificationResult
                        {
                            CriterionId = criterion.CriterionId,
                            RawScore = 7m,
                            Reason = "reason",
                            Evidence = "answer",
                            EvidenceSource = EvidenceSourceKind.PrimaryAnswer,
                            EvidenceSourceColumnId = "A",
                        })
                        .ToImmutableArray(),
                }));
        }
    }

    private sealed class ReferenceRunner(bool fail) : IReferenceAnswerOperationRunner
    {
        public Task<AuxiliaryOperationResult<ReferenceAnswerResult>> EvaluateAsync(SafeReferenceAnswerPayload payload, CancellationToken cancellationToken) =>
            Task.FromResult(fail
                ? AuxiliaryOperationResult<ReferenceAnswerResult>.Failed(ResultsStatusCodes.AiTimeout, attemptCount: 3)
                : AuxiliaryOperationResult<ReferenceAnswerResult>.Succeeded(new ReferenceAnswerResult { QuestionId = payload.QuestionId, Answer = ReferenceText }));
    }

    private sealed class SpecialRunner : ISpecialEvaluationOperationRunner
    {
        public Task<AuxiliaryOperationResult<SpecialQuantificationResult>> EvaluateAsync(SafeSpecialEvaluationPayload payload, string modelId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("no special evaluation");
    }

    private static T Prop<T>(object target, string name)
    {
        PropertyInfo? property = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        Assert.True(property is not null, $"契約: {target.GetType().Name}.{name} が必要（FR-114 AC-010/AC-077 の診断項目）");
        object? value = property!.GetValue(target);
        Assert.IsType<T>(value);
        return (T)value!;
    }

    private static void TrySelect(ResultsOutputViewModel vm, ResultsRowScoreViewModel row)
    {
        PropertyInfo? selected = vm.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(p => p.CanWrite && p.PropertyType == typeof(ResultsRowScoreViewModel));
        try
        {
            selected?.SetValue(vm, row);
        }
        catch (TargetInvocationException)
        {
        }
    }

    // 公開プロパティを辿って文字列を集める（画面・読込結果に表示される文言の確認用）。
    private static List<string> CollectStrings(object root, int depth)
    {
        List<string> found = [];
        HashSet<object> seen = new(ReferenceEqualityComparer.Instance);
        void Walk(object? value, int level)
        {
            if (value is null || found.Count > 20_000)
            {
                return;
            }

            if (value is string s)
            {
                found.Add(s);
                return;
            }

            Type type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || value is decimal || value is DateTime || value is DateTimeOffset || value is Delegate || level > depth || !seen.Add(value))
            {
                return;
            }

            if (value is IEnumerable sequence)
            {
                int n = 0;
                foreach (object? item in sequence)
                {
                    if (n++ > 500)
                    {
                        break;
                    }

                    Walk(item, level + 1);
                }

                return;
            }

            if (type.Namespace is null || !type.Namespace.StartsWith("StudyReportEvaluator", StringComparison.Ordinal))
            {
                return;
            }

            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                object? child;
                try
                {
                    child = property.GetValue(value);
                }
                catch (TargetInvocationException)
                {
                    continue;
                }

                Walk(child, level + 1);
            }
        }

        Walk(root, 0);
        return found;
    }

    private static string RemoveTopLevelProperty(string json, string name)
    {
        System.Text.Json.Nodes.JsonObject node = System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();
        Assert.True(node.Remove(name));
        return node.ToJsonString();
    }

    private static string ReplaceTopLevelNumber(string json, string name, string rawJsonValue)
    {
        System.Text.Json.Nodes.JsonObject node = System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();
        node[name] = System.Text.Json.Nodes.JsonNode.Parse(rawJsonValue);
        return node.ToJsonString();
    }

    private static decimal ParseDec(string? text)
    {
        Assert.False(string.IsNullOrEmpty(text), "値が blank");
        return decimal.Parse(text!, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    private static string Sha(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static string RepoRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "StudyReportEvaluator.slnx")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new InvalidOperationException("repository root not found");
    }

    private static List<string> AllCellTexts(string path)
    {
        using SpreadsheetDocument doc = SpreadsheetDocument.Open(path, false);
        List<string> texts = [];
        foreach (WorksheetPart part in doc.WorkbookPart!.WorksheetParts)
        {
            foreach (Cell cell in part.Worksheet!.Descendants<Cell>())
            {
                string? text = CellText(doc, cell);
                if (!string.IsNullOrEmpty(text))
                {
                    texts.Add(text);
                }
            }

            foreach (var comment in part.WorksheetCommentsPart?.Comments?.Descendants<CommentText>() ?? [])
            {
                texts.Add(comment.InnerText);
            }
        }

        foreach (var name in doc.WorkbookPart.Workbook!.Sheets!.Elements<Sheet>())
        {
            texts.Add(name.Name!.Value!);
        }

        return texts;
    }

    private static WorksheetPart Sheet(SpreadsheetDocument doc, string name)
    {
        Sheet sheet = doc.WorkbookPart!.Workbook!.Sheets!.Elements<Sheet>().Single(s => s.Name == name);
        return (WorksheetPart)doc.WorkbookPart.GetPartById(sheet.Id!.Value!);
    }

    private static Dictionary<string, string> HeaderMap(SpreadsheetDocument doc, WorksheetPart part)
    {
        Dictionary<string, string> map = [];
        Row? header = part.Worksheet!.Descendants<Row>().FirstOrDefault(r => r.RowIndex!.Value == 1);
        foreach (Cell cell in header?.Elements<Cell>() ?? [])
        {
            string? text = CellText(doc, cell);
            if (!string.IsNullOrEmpty(text))
            {
                map[text] = CellRef().Match(cell.CellReference!.Value!).Groups[1].Value;
            }
        }

        return map;
    }

    private static string? CellByHeader(SpreadsheetDocument doc, string sheetName, Dictionary<string, string> headers, int sourceRow, string header)
    {
        WorksheetPart part = Sheet(doc, sheetName);
        string sourceColumn = headers["SourceRow"];
        foreach (Row r in part.Worksheet!.Descendants<Row>().Where(r => r.RowIndex!.Value > 1))
        {
            Cell? source = r.Elements<Cell>().FirstOrDefault(c => c.CellReference == sourceColumn + r.RowIndex!.Value);
            if (source is not null && CellText(doc, source) == sourceRow.ToString(CultureInfo.InvariantCulture))
            {
                Cell? cell = r.Elements<Cell>().FirstOrDefault(c => c.CellReference == headers[header] + r.RowIndex!.Value);
                return cell is null ? null : CellText(doc, cell);
            }
        }

        return null;
    }

    private static string? CellText(SpreadsheetDocument doc, Cell cell)
    {
        if (cell.DataType?.Value == CellValues.InlineString)
        {
            return cell.InlineString?.InnerText;
        }

        string? value = cell.CellValue?.Text;
        if (value is not null && cell.DataType?.Value == CellValues.SharedString)
        {
            SharedStringTablePart? table = doc.WorkbookPart!.SharedStringTablePart;
            return table?.SharedStringTable!.Elements<SharedStringItem>().ElementAt(int.Parse(value, CultureInfo.InvariantCulture)).InnerText;
        }

        return value;
    }

    private static Cell Inline(string reference, string value) =>
        new()
        {
            CellReference = reference,
            DataType = CellValues.InlineString,
            InlineString = new InlineString(new Text(value)),
        };

    private sealed class TempDir : IDisposable
    {
        internal string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SRE-ST114-" + Guid.NewGuid().ToString("N"));

        internal TempDir() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
