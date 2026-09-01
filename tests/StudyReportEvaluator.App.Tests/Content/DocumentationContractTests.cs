using System.Text.RegularExpressions;
using Xunit;

namespace StudyReportEvaluator.App.Tests.Content;

public sealed class DocumentationContractTests
{
    private static readonly string[] V4BaselineDocumentPaths =
    [
        "docs/requirements-definition.md",
        "docs-dev/README.md",
        "docs-dev/architecture.md",
        "docs-dev/detailed-design.md",
        "docs-dev/excel-contract.md",
        "docs-dev/traceability.md",
        "docs-dev/adr/0012-point-allocation-similarity-resume-portability.md",
        "work/20260901-v4-implementation-plan.md",
    ];

    private static readonly string[] ExistingUserDocumentPaths =
    [
        "README.md",
        "docs/README.md",
        "docs/getting-started.md",
        "docs/features.md",
        "docs/custom-evaluator-guide.md",
        "docs/prompt-launch.md",
        "docs/privacy-and-data-handling.md",
        "docs/troubleshooting.md",
        "images/README.md",
    ];

    [Fact]
    public void V4_baseline_and_existing_user_documents_exist_and_are_nonempty()
    {
        foreach (string relativePath in V4BaselineDocumentPaths.Concat(ExistingUserDocumentPaths))
        {
            string path = Resolve(FindRepositoryRoot(), relativePath);
            Assert.True(File.Exists(path), $"Missing documentation source: {relativePath}");
            Assert.False(string.IsNullOrWhiteSpace(File.ReadAllText(path)));
        }
    }

    [Fact]
    public void Requirement_v4_records_approved_defaults_formulas_workflow_and_scope()
    {
        string requirements = Read("docs/requirements-definition.md");

        AssertContainsAll(
            requirements,
            "| 文書版 | 4.0 |",
            "| 状態 | 要求所有者承認済み baseline |",
            "ベース点 `BasePoints`。既定60",
            "固有設定配点 `SpecialPoints`。既定0",
            "類似度減点係数 `SimilarityPenaltyWeight`。既定0.1",
            "B+S+\\sum_{q=1}^{N}P_q=100",
            "QuestionEarned_q=P_qR_q",
            "SimilarityPenalty_q=P_qL_qW",
            "FinalRaw=B+\\sum_q QuestionEarned_q+SpecialEarned-\\sum_q SimilarityPenalty_q",
            "FinalScore=",
            "0 & FinalRaw<0",
            "100 & FinalRaw>100",
            "FinalRaw & \\text{otherwise}",
            "`eval-{yyyyMMdd-HHmm}-02.xlsx`",
            "`eval-{yyyyMMdd-HHmm}.partial.xlsx`",
            "`Quantification_References`",
            "`Quantification_Checkpoint`",
            "StudyReportEvaluator.App --input <xlsx-path> --prompt <txt-path>",
            "Windows 11 x64",
            "macOS arm64 / x64",
            "NOT_RUN_EXTERNAL_PREREQUISITE",
            "## 22. Approval record");

        AssertSequentialTableIds(requirements, "AC-", 22);
        Assert.Contains("1. Microsoft Forms型、Google Forms型", requirements, StringComparison.Ordinal);
        Assert.Contains("24. optional authenticated synthetic Copilot smoke", requirements, StringComparison.Ordinal);
    }

    [Fact]
    public void Current_developer_index_uses_v4_and_adr0012_as_sources_of_truth()
    {
        string index = Read("docs-dev/README.md");
        string architecture = Read("docs-dev/architecture.md");

        AssertContainsAll(
            index,
            "[詳細設計書](detailed-design.md)",
            "[ADR-0012](adr/0012-point-allocation-similarity-resume-portability.md)",
            "current requirements v4.0 / ADR-0012 / detailed design");
        AssertContainsAll(
            architecture,
            "| Current requirement | requirements v4.0 |",
            "| Current decision | ADR-0012 |",
            "ReferenceCheckpoint",
            "complete-row checkpoint",
            "submit_reference_answer",
            "submit_special_quantification",
            "submit_similarity",
            "technical failureはblank",
            "partial | `Quantification_Checkpoint`",
            "final | `Quantification_Config`, `Quantification_References`, `Quantification_Results`, `Quantification_Run`");
    }

    [Fact]
    public void Adr_and_detailed_design_preserve_minimal_architecture_and_explicit_boundaries()
    {
        string adr = Read("docs-dev/adr/0012-point-allocation-similarity-resume-portability.md");
        string design = Read("docs-dev/detailed-design.md");

        AssertContainsAll(
            adr,
            "| 状態 | **承認済み** |",
            "StudyReportEvaluator.Core",
            "StudyReportEvaluator.App",
            "production projectは次の2件だけ",
            "questionの旧`Weight`は`Points`へ置き換え",
            "SpecialEvaluationDefinition",
            "1 question/runで1回",
            "Copilot session persistenceをjob resumeに使用しない",
            "汎用plugin typeは導入しない",
            "SQLite／cloud database");
        AssertContainsAll(
            design,
            "public decimal BasePoints { get; init; } = 60m;",
            "public decimal SpecialPoints { get; init; }",
            "public decimal SimilarityPenaltyWeight { get; init; } = 0.1m;",
            "public decimal Points { get; init; }",
            "SpecialEvaluationDefinition",
            "ScoringAllocationCalculator",
            "Core result型へApp型を参照させない",
            "row間は並列化しない",
            "1行内のnormal evaluator、special item、similarity");
    }

    [Fact]
    public void Excel_contract_defines_partial_final_sheets_and_blank_safe_formulas()
    {
        string contract = Read("docs-dev/excel-contract.md");

        AssertContainsAll(
            contract,
            "`eval-20260901-1530.partial.xlsx`",
            "input byte-copy + `Quantification_Checkpoint`",
            "input byte-copy + Config / References / Results / Run",
            "`PayloadSha256`",
            "replace前の失敗では旧partialを保持",
            "`Quantification_References`",
            ".Question_Earned",
            ".Special_Question_Rate",
            ".Similarity_Penalty",
            "`Final_Raw`",
            "`Final_Score`",
            "EnabledSpecialCount >= 1",
            "COUNT(...)=expected",
            "0は数値として数え、blankは数えない",
            "MidpointRounding.AwayFromZero",
            "same-volume no-overwrite move");
        AssertDoesNotContainAny(contract, "MIN(`", "MAX(`", "AVERAGE(`", "COUNTIF(`");
    }

    [Fact]
    public void Plan_and_traceability_map_every_task_acceptance_and_test_requirement()
    {
        string plan = Read("work/20260901-v4-implementation-plan.md");
        string traceability = Read("docs-dev/traceability.md");

        foreach (string task in new[]
        {
            "B-01", "B-06", "C-01", "C-06", "X-01", "X-04", "A-01", "A-04",
            "W-01", "W-02", "U-01", "U-04", "L-01", "P-01", "P-03", "D-01",
            "D-05", "E-01", "E-03",
        })
        {
            Assert.Matches(
                $@"(?m)^\| {Regex.Escape(task)}(?:\s|\|)",
                plan);
        }

        AssertSequentialTableIds(traceability, "AC-", 22);
        AssertSequentialTableIds(traceability, "TR-", 24);
        AssertContainsAll(
            traceability,
            "IMPLEMENTATION_IN_PROGRESS",
            "`PLANNED`は未実装をPASSと称しない",
            "BLOCKED_REVIEW",
            "NOT_RUN_EXTERNAL_PREREQUISITE",
            "unresolved reproducible blocker/high finding 0");
    }

    [Fact]
    public void Historical_v3_decision_is_retained_but_not_current()
    {
        string adr11 = Read("docs-dev/adr/0011-dynamic-quantification-excel-formulas.md");
        string adr12 = Read("docs-dev/adr/0012-point-allocation-similarity-resume-portability.md");
        string developerIndex = Read("docs-dev/README.md");

        Assert.Contains("| 状態 | **承認済み** |", adr11, StringComparison.Ordinal);
        Assert.Contains("Supersedes | ADR-0011", adr12, StringComparison.Ordinal);
        Assert.Contains("ADR-0001〜0011", developerIndex, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "current requirements v3.0",
            developerIndex,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void V4_baseline_never_promotes_unexecuted_external_evidence_to_pass()
    {
        string content = string.Join(Environment.NewLine, V4BaselineDocumentPaths.Select(Read));

        AssertContainsAll(
            content,
            "NOT_RUN_EXTERNAL_PREREQUISITE",
            "未実行をPASSとしない",
            "署名済み／notarized／対応済みと称しない");
        AssertDoesNotContainAny(
            content,
            "macOS signing PASS",
            "macOS notarization PASS",
            "signed and notarized artifact is verified");
    }

    [Fact]
    public void Prompt_launch_guide_has_copyable_teacher_scenarios_and_never_claims_unattended_grading()
    {
        string guide = Read("docs/prompt-launch.md");
        string root = Read("README.md");
        string userIndex = Read("docs/README.md");
        string gettingStarted = Read("docs/getting-started.md");
        string customGuide = Read("docs/custom-evaluator-guide.md");

        AssertContainsAll(
            root,
            "[GitHub CopilotからPromptで起動する](docs/prompt-launch.md)");
        AssertContainsAll(
            userIndex,
            "[GitHub CopilotからPromptで起動する](prompt-launch.md)");
        AssertContainsAll(gettingStarted, "[GitHub CopilotからPromptで起動する](prompt-launch.md)");
        AssertContainsAll(customGuide, "[GitHub CopilotからPromptで起動する](prompt-launch.md)");
        AssertContainsAll(
            guide,
            "## できることと安全上の境界",
            "**Copilot起動依頼Prompt**",
            "**評価Promptファイル**",
            "--input",
            "--prompt",
            "AI評価を自動開始しません",
            "定量化を開始",
            "result/eval-{yyyyMMdd-HHmm}[-NN].xlsx",
            "sample/機械学習 サブフィールド PBL 2025 レポート - コピー.xlsx",
            "| F | 設問1のレポート回答primary候補 |",
            "| G | 設問1に関する学生Prompt候補 |",
            "| I | 設問2のレポート回答primary候補 |",
            "| J | 設問2に関する学生Prompt候補 |",
            "| K | Prompt作成時の考慮事項／観点。Jのsupporting候補 |",
            "## ユースケース1 — 機械学習概念の理解を確認する",
            "## ユースケース2 — PBL提案の具体性と実行可能性を確認する",
            "## ユースケース3 — 学生が作成したPromptの品質を固有評価する",
            "## ユースケース4 — Prompt作成時の考慮事項を含めて評価する",
            "## ユースケース5 — まず10行だけpilot実行する",
            "## ユースケース6 — 中断したrunを再開する",
            "ml-concept-understanding.txt",
            "student-prompt-quality.txt",
            "{回答}",
            "{評価項目}",
            "## GitHub Copilotへ結果fileの存在だけ確認してもらう",
            "workbookを開いてcell、sheet本文、氏名、回答、score、reason、evidenceを読むこと",
            "存在しないfileを作成済みと報告すること",
            "類似度は不正行為の証明ではありません",
            "最終的な評点と利用判断は教員が行います");
    }

    [Fact]
    public void System_test_prompt_maps_all_v4_test_requirements_without_stale_v3_expectations()
    {
        string prompt = Read("tests/system-test-prompt.md");

        int[] promptIds = Regex.Matches(
                prompt,
                @"^### STP-TR-(\d{2}):",
                RegexOptions.Multiline | RegexOptions.CultureInvariant)
            .Select(match => int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();

        Assert.Equal(Enumerable.Range(1, 24), promptIds);
        foreach (int requirementId in Enumerable.Range(1, 24))
        {
            string promptBody = ExtractSystemTestPrompt(prompt, requirementId);
            AssertContainsAll(
                promptBody,
                $"Test ID: STP-TR-{requirementId:00}",
                $"Requirement: TR-{requirementId:00}",
                "厳守:",
                "報告順:");
        }

        AssertContainsAll(
            prompt,
            "| 対象 | StudyReport Evaluator v4.0 |",
            "**24件**を正本とする");
        Assert.Matches(@"BasePoints\s*=\s*60", ExtractSystemTestPrompt(prompt, 4));
        Assert.Matches(@"SpecialPoints\s*=\s*0", ExtractSystemTestPrompt(prompt, 4));
        Assert.Matches(@"SimilarityPenaltyWeight\s*=\s*0\.1", ExtractSystemTestPrompt(prompt, 4));
        AssertContainsAll(
            ExtractSystemTestPrompt(prompt, 6),
            "6 placeholder制約は利用者編集可能なCustom／Specialだけに適用する",
            "`{参照回答}`を利用者placeholderとして許可しない",
            "reference=`submit_reference_answer`",
            "similarity=`submit_similarity`");
        AssertContainsAll(
            ExtractSystemTestPrompt(prompt, 8),
            "空normal primaryはAI callなし、QuestionRate=0、QuestionEarned=0",
            "nonempty入力のschema/timeout/network/auth/cleanup failureはraw blankで、0へ変換しない");
        AssertContainsAll(
            ExtractSystemTestPrompt(prompt, 11),
            "Config、References、Results、Runのexact 4件",
            "finalにはCheckpoint sheetがない");
        AssertContainsAll(
            ExtractSystemTestPrompt(prompt, 13),
            "`Quantification_Checkpoint`",
            "reference完了およびcomplete student rowごと");
        AssertContainsAll(
            ExtractSystemTestPrompt(prompt, 14),
            "complete rowだけskipし、最初の未完了rowから続行する",
            "row途中の結果はskipせず、そのrow全体を再実行する");
        AssertContainsAll(
            ExtractSystemTestPrompt(prompt, 17),
            "生成AIが行う評価には正確性が欠ける可能性があるため、必ず自分で責任をもって評点を行ってください。このツールや生成AIは評価結果に対しては一切の責任を負えません");
        AssertContainsAll(
            ExtractSystemTestPrompt(prompt, 18),
            "`--input` 0/1回と`--prompt` 0回以上",
            "startup後のCopilot runner/session call countは0");
        AssertContainsAll(
            ExtractSystemTestPrompt(prompt, 19),
            "bundled CLI",
            "PATH fallbackしない");
        AssertContainsAll(
            ExtractSystemTestPrompt(prompt, 21),
            "NOT_RUN_EXTERNAL_PREREQUISITE",
            "未実行をPASSにしない");
        AssertContainsAll(
            ExtractSystemTestPrompt(prompt, 24),
            "required deterministic acceptanceの代替にしない",
            "A/Bを別statusで記録し、片方のPASSをもう片方またはrequired gateへ代用しない");
        AssertDoesNotContainAny(
            prompt,
            "| 対象 | StudyReport Evaluator v3.0 実装 |",
            "空回答、失敗、取消、欠損scoreは0ではなく空欄",
            "Config、Results、Runの3 sheet",
            "`tests/fixtures/v3/definition-matrix.json`",
            "`tests/fixtures/v3/result-oracles.json`",
            "signed installer、signed ZIP、notarization");
    }

    private static string ExtractSystemTestPrompt(string content, int requirementId)
    {
        Match match = Regex.Match(
            content,
            $@"(?ms)^### STP-TR-{requirementId:00}:.*?^```text\r?\n(?<body>.*?)^```",
            RegexOptions.CultureInvariant);
        Assert.True(match.Success, $"Missing copyable Prompt body for STP-TR-{requirementId:00}.");
        return match.Groups["body"].Value;
    }

    private static void AssertSequentialTableIds(string content, string prefix, int expectedCount)
    {
        int[] actual = Regex.Matches(
                content,
                $@"^\| {Regex.Escape(prefix)}(\d{{2,3}}) \|",
                RegexOptions.Multiline | RegexOptions.CultureInvariant)
            .Select(match => int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();

        Assert.Equal(Enumerable.Range(1, expectedCount), actual);
    }

    private static void AssertContainsAll(string content, params string[] expectedFragments)
    {
        foreach (string fragment in expectedFragments)
        {
            Assert.Contains(fragment, content, StringComparison.Ordinal);
        }
    }

    private static void AssertDoesNotContainAny(string content, params string[] forbiddenFragments)
    {
        foreach (string fragment in forbiddenFragments)
        {
            Assert.DoesNotContain(fragment, content, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string Read(string relativePath) =>
        File.ReadAllText(Resolve(FindRepositoryRoot(), relativePath));

    private static string Resolve(string root, string relativePath) =>
        Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "StudyReportEvaluator.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            "The repository root containing StudyReportEvaluator.slnx was not found.");
    }
}
