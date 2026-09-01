using Xunit;

namespace StudyReportEvaluator.App.Tests.Content;

public sealed class DocumentationContractTests
{
    private static readonly string[] CurrentDocumentPaths =
    [
        "README.md",
        "docs/README.md",
        "docs/getting-started.md",
        "docs/features.md",
        "docs/custom-evaluator-guide.md",
        "docs/privacy-and-data-handling.md",
        "docs/troubleshooting.md",
        "docs/requirements-definition.md",
        "docs-dev/README.md",
        "docs-dev/implementation-status.md",
        "docs-dev/architecture.md",
        "docs-dev/excel-contract.md",
        "docs-dev/traceability.md",
        "images/README.md",
    ];

    [Fact]
    public void D01_source_documents_exist()
    {
        string root = FindRepositoryRoot();

        foreach (string relativePath in CurrentDocumentPaths)
        {
            string path = Resolve(root, relativePath);
            Assert.True(File.Exists(path), $"Missing D-01 source document: {relativePath}");
            Assert.False(string.IsNullOrWhiteSpace(File.ReadAllText(path)));
        }
    }

    [Fact]
    public void Readme_preserves_project_context_and_removes_preimplementation_status()
    {
        string readme = Read("README.md");

        AssertContainsAll(
            readme,
            "Excel の学習レポートの採点を数値化・定量化するツールです。",
            "[はじめに](docs/getting-started.md)",
            "[機能リファレンス](docs/features.md)",
            "[Custom evaluatorガイド](docs/custom-evaluator-guide.md)",
            "[データとprivacy](docs/privacy-and-data-handling.md)",
            "[トラブルシューティング](docs/troubleshooting.md)",
            "[要求定義書](docs/requirements-definition.md)",
            "[アーキテクチャ](docs-dev/architecture.md)",
            "[Excel / formula 契約](docs-dev/excel-contract.md)",
            "[現在の実装状態](docs-dev/implementation-status.md)",
            "このリポジトリへ実在する学生の回答・氏名・メールアドレス等を追加しないでください。");

        AssertDoesNotContainAny(
            readme,
            "アプリ実装はまだ開始していません",
            "全 P0 ゲートを満たすまで",
            "実データ処理の安全性・妥当性を実装・検証済みではありません",
            "実装後に実物を使って本 README へ追加します");
    }

    [Fact]
    public void Readme_documents_supported_scope_workflow_and_exact_commands()
    {
        string readme = Read("README.md");

        AssertContainsAll(
            readme,
            "Windows 11 x64",
            ".NET 10 / Avalonia",
            "標準 `.xlsx` 1ファイルのみ",
            "AI定量化を実行するときだけ、既存の GitHub Copilot CLI ログインを使用",
            "Microsoft Excel / Office / LibreOffice / COM automation は不要",
            "現在のUIにnative file pickerはありません",
            "`copilot.exe`はZIPへ同梱されません",
            "Windowsの`PATH`から解決できる状態",
            "1. **入力**",
            "2. **定量化設計**",
            "3. **実行**",
            "4. **結果・出力**",
            "dotnet restore StudyReportEvaluator.slnx --locked-mode",
            "dotnet build StudyReportEvaluator.slnx --no-restore -c Release",
            "dotnet test StudyReportEvaluator.slnx --no-build --no-restore -c Release",
            "pwsh.exe -NoLogo -NoProfile -File scripts/publish-windows.ps1",
            "pwsh.exe -NoLogo -NoProfile -File scripts/package-windows.ps1",
            "artifacts/package/StudyReportEvaluator-win-x64.zip",
            "artifacts/package/StudyReportEvaluator-win-x64.zip.sha256",
            "generated ignored artifact",
            "unsigned");
    }

    [Fact]
    public void Readme_records_output_security_and_evidence_without_promoting_optional_smokes()
    {
        string readme = Read("README.md");

        AssertContainsAll(
            readme,
            "Quantification_Config",
            "Quantification_Results",
            "Quantification_Run",
            "cached value",
            "atomic rename",
            "app-owned database と cloud backend はありません",
            "sample/機械学習 サブフィールド PBL 2025 レポート - コピー.xlsx",
            "446386E20BB4096561CB4AFD6D74B8EAA9D50EAE53C97F984BA7F70EBEAD0DE5",
            "solution tests 452件成功",
            "test runごとに再測定",
            "固定保証値ではない",
            "optional live Copilot smoke | `PASS`",
            "optional external recalculation smoke | `PASS`",
            "固定合成payload 1件だけ",
            "固定合成workbook",
            "合成100名 × 2設問から7枚を生成",
            "[標準xlsxのpath、sheet、行範囲を設定する入力画面](images/01-input-workbook.png)",
            "[Auto、concurrency 2、200 evaluation unitsを示す実行画面](images/05-execution-auto.png)",
            "入力workbook全体のbyte-copy",
            "入力と同等以上に機密",
            "question text、criterion ID",
            "## 100名 × 2設問のトークン計画値（Auto）",
            "200 evaluation units",
            "このケースの実測済みexact token総数はありません",
            "240,000",
            "480,000",
            "528,000",
            "1,440,000",
            "全unitが最大3 attempts",
            "token数自体を10%減らすという意味ではありません",
            "Quantification_Run`へ数値だけを保存",
            "65,536 scalars以下",
            "model prompt/context上限の80%以内",
            "ZIPには`RELEASE-NOTES.txt`に加えて、本`README.md`、利用者向け`docs/`、合成画面の`images/`を同梱",
            "Usage and billing",
            "Auto model selection",
            "IMPL-GAP-001",
            "IMPL-GAP-002");

        Assert.DoesNotContain("ZIP は commit 済み", readme, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("443件すべて成功", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("3.279264 秒", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void Architecture_documents_exact_topology_boundaries_and_flows()
    {
        string architecture = Read("docs-dev/architecture.md");

        AssertContainsAll(
            architecture,
            "production project は正確に2件、test project は正確に2件",
            "src/StudyReportEvaluator.Core/",
            "src/StudyReportEvaluator.App/",
            "tests/StudyReportEvaluator.Core.Tests/",
            "tests/StudyReportEvaluator.App.Tests/",
            "BCL-only",
            "Avalonia",
            "DocumentFormat.OpenXml",
            "GitHub.Copilot.SDK",
            "flowchart LR",
            "flowchart TD",
            "sequenceDiagram",
            "immutable snapshot",
            "canonical JSON",
            "SHA-256",
            "submit_quantification",
            "target-local",
            "no-overwrite atomic rename",
            "app-owned database と cloud backend はありません",
            "native pickerではなくfull pathのTextBox",
            "Reason / Evidence / Question scoreはworkbookだけ",
            "IMPL-GAP-001 / 002",
            "run admission",
            "最大20,000 attempts",
            "observed numeric usage",
            "optional live Copilot smokeとoptional Microsoft Excel recalculation smokeは、固定合成データだけで`PASS`",
            "required test、実在データ品質、全環境保証へ読み替えません");
    }

    [Fact]
    public void Excel_document_defines_literals_formulas_rounding_preflight_and_atomicity()
    {
        string contract = Read("docs-dev/excel-contract.md");

        AssertContainsAll(
            contract,
            "`RawWeight`（R列）",
            "`EffectiveMinimum`（S列）",
            "`EffectiveMaximum`（T列）",
            "`RoundingDigits`（U列）",
            ".Scorable",
            ".AI_Raw",
            ".Override",
            ".Effective_Raw",
            ".Normalized",
            ".Reason",
            ".Evidence",
            ".Evidence_Source",
            ".Evidence_SourceColumn",
            ".Status",
            ".Evaluator_Score",
            ".Question_Score",
            "Overall_Score",
            "=IF(ScorableCell<>1",
            "=IF(ISNUMBER(EffectiveRawCell)",
            "=IF(COUNT(ChildScoreRef1,...)=EnabledChildCount",
            "AI rawへfallbackせず",
            "MidpointRounding.AwayFromZero",
            "cached value",
            "`IF`, `IFERROR`, `ISNUMBER`, `COUNT`, `SUM`, `SUMPRODUCT`, `ROUND`",
            "最大8,191文字",
            "8,192文字以上",
            "32,767文字以下",
            "Quantification_Config (2)",
            "target-local",
            "no-overwrite atomic rename",
            "SHA-256、size、last-write time",
            "Office-independent required path",
            "Current timing",
            "AI run開始前",
            "入力と同等以上に機密",
            "Working with formulas",
            "advisoryなoptional smokeとして`PASS`",
            "required oracleや全環境保証を代替しません");
    }

    [Fact]
    public void Custom_guide_has_exactly_six_placeholders_and_safe_rendering_contract()
    {
        string guide = Read("docs/custom-evaluator-guide.md");
        string[] catalog = guide
            .Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => line.StartsWith("| `{", StringComparison.Ordinal))
            .Select(line => line.Split('|')[1].Trim().Trim('`'))
            .ToArray();

        Assert.Equal(
            ["{設問}", "{回答}", "{補助情報}", "{評価項目}", "{最小点}", "{最大点}"],
            catalog);
        AssertContainsAll(
            guide,
            "Knowledge (`KNOWLEDGE_COVERAGE`)",
            "Custom (`CUSTOM_PROMPT`)",
            "任意の1列をprimary column",
            "0件以上の列をsupporting columns",
            "`{回答}` と `{評価項目}` をそれぞれ1回以上",
            "`{{` と `}}` でescape",
            "挿入値は **opaque**",
            "再走査しません",
            "evaluator default range",
            "criterion effective range",
            "個別weight / weight合計",
            "実効percentage",
            "安全なtemplate例",
            "TEMPLATE_REQUIRED",
            "ANSWER_PLACEHOLDER_REQUIRED",
            "CRITERIA_PLACEHOLDER_REQUIRED",
            "UNKNOWN_PLACEHOLDER",
            "UNCLOSED_PLACEHOLDER",
            "MALFORMED_PLACEHOLDER",
            "UNMATCHED_CLOSING_BRACE",
            "AIへaggregateを要求しないでください",
            "mandatory human review",
            "input capture、row read、Copilot session、runner callを開始しません");
    }

    [Fact]
    public void User_documents_describe_actual_path_entry_result_surface_and_data_boundary()
    {
        string gettingStarted = Read("docs/getting-started.md");
        string features = Read("docs/features.md");
        string privacy = Read("docs/privacy-and-data-handling.md");
        string troubleshooting = Read("docs/troubleshooting.md");

        AssertContainsAll(
            gettingStarted,
            "現在のUIにnative file pickerはありません",
            "Reason、Evidence、Evidence source、Question score",
            "同一プロセス内で完了またはcancelされたrun",
            "../images/01-input-workbook.png",
            "../images/02-input-mapping.png",
            "../images/03-design-knowledge.png",
            "../images/04-design-custom-prompt.png",
            "../images/05-execution-auto.png",
            "../images/06-results-review.png",
            "../images/07-output-export.png",
            "sequenceDiagram");
        AssertContainsAll(
            features,
            "保存済み結果の再import",
            "reusable definition profile",
            "画面とworkbookの項目差",
            "AI_RUNTIME_FAILED");
        AssertContainsAll(
            privacy,
            "definition由来",
            "question text",
            "入力と同等以上に機密",
            "他行、非選択列、workbook path");
        AssertContainsAll(
            troubleshooting,
            "100 MiB",
            "InvalidRelationship",
            "REQUEST_SCALAR_LIMIT_EXCEEDED",
            "REQUEST_CONTEXT_BUDGET_EXCEEDED",
            "ATTEMPT_BUDGET_TOO_LARGE",
            "Excel specifications and limits");

        string imageManifest = Read("images/README.md");
        AssertContainsAll(
            imageManifest,
            "Avalonia 12.1.1 Headless + Skia",
            "fake authentication boundary",
            "personal/student data: なし",
            "01-input-workbook.png",
            "07-output-export.png",
            "retry込み最大600 attempts",
            "STUDY_REPORT_EVALUATOR_GENERATE_DOC_IMAGES");
    }

    [Fact]
    public void Frozen_requirement_current_status_and_historical_documents_are_explicitly_separated()
    {
        string requirements = Read("docs/requirements-definition.md");
        string implementationStatus = Read("docs-dev/implementation-status.md");
        string traceability = Read("docs-dev/traceability.md");
        string developerIndex = Read("docs-dev/README.md");
        string historicalAdr = Read("docs-dev/adr/0005-copilot-result-protocol.md");
        string historicalPreflight = Read("docs-dev/preflight/governance-inputs.md");
        string historicalRelease = Read("docs-dev/release/p1-disposition.md");

        AssertContainsAll(
            requirements,
            "| 文書版 | 3.0 |",
            "動的Prompt定量化・Excel加重計算版（実装前）",
            "## 19. Traceability summary");
        AssertContainsAll(
            implementationStatus,
            "Historical GATE-ACCEPTANCE record | `PASS`",
            "Documentation refresh validation | post-gateで文書契約test 2件とvisual documentation test 1件を追加後、Release build PASS、455 passed / 0 failed / 0 skipped",
            "Gap closure non-document validation | Release build PASS、473 passed / 0 failed / 0 skipped",
            "Current required validation | locked restore PASS、Release build PASS、485 passed / 0 failed / 0 skipped",
            "Post-gate conformance gaps | 0 open / 2 closed",
            "## IMPL-GAP-001 — CLOSED",
            "## IMPL-GAP-002 — CLOSED");
        AssertContainsAll(
            traceability,
            "PASS recorded for HEAD `62581a3`",
            "PASS_RECORDED_POST_GATE_GAP_OPEN",
            "GATE-ACCEPTANCE PASS at 62581a3",
            "CLOSED_PENDING_NEW_ACCEPTANCE",
            "IMPL-GAP-001 / 002 closure");
        AssertContainsAll(
            developerIndex,
            "## 現行正本",
            "## 履歴文書",
            "Source of truthの優先順位",
            "exact-byte固定したv3.0規範baseline",
            "実装後のstatusと差分を`implementation-status.md`へ分離");
        Assert.Contains("HISTORICAL / SUPERSEDED", historicalAdr, StringComparison.Ordinal);
        Assert.Contains("HISTORICAL / OUTSIDE CURRENT SCOPE", historicalPreflight, StringComparison.Ordinal);
        Assert.Contains("HISTORICAL / SUPERSEDED", historicalRelease, StringComparison.Ordinal);
    }

    [Fact]
    public void Warning_is_exact_and_nonblocking_where_user_behavior_is_documented()
    {
        const string warning =
            "AIによる定量値には誤りや偏りが含まれる可能性があります。利用目的に応じて結果を確認してください。";

        foreach (string relativePath in new[]
        {
            "README.md",
            "docs-dev/architecture.md",
            "docs/custom-evaluator-guide.md",
        })
        {
            string content = Read(relativePath);
            Assert.Contains(warning, content, StringComparison.Ordinal);
            Assert.Contains("nonblocking", content, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Documentation_has_no_unsupported_claim_or_unavailable_screenshot_reference()
    {
        string allDocuments = string.Join(
            Environment.NewLine,
            CurrentDocumentPaths.Select(Read));

        AssertDoesNotContainAny(
            allDocuments,
            "署名済み ZIP",
            "署名済みパッケージ",
            "デジタル署名済み",
            "Signing: SIGNED",
            "macOS 対応",
            "Linux 対応",
            "Windows Arm64 対応",
            "教育的妥当性を保証します",
            "教育的妥当性は保証済み",
            "公平性を保証します",
            "公平性は保証済み",
            "法的適合性を保証します",
            "法的適合性は保証済み",
            "品質を保証します",
            "品質は保証済み");
        AssertContainsAll(
            allDocuments,
            "fixed synthetic payload",
            "required substitute false",
            "固定合成workbook");
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
