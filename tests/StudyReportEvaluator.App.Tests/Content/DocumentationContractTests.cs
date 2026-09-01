using Xunit;

namespace StudyReportEvaluator.App.Tests.Content;

public sealed class DocumentationContractTests
{
    private static readonly string[] D01DocumentPaths =
    [
        "README.md",
        "docs-dev/architecture.md",
        "docs-dev/excel-contract.md",
        "docs-dev/custom-evaluator-guide.md",
    ];

    [Fact]
    public void D01_source_documents_exist()
    {
        string root = FindRepositoryRoot();

        foreach (string relativePath in D01DocumentPaths)
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
            "[要求定義書](docs/requirements-definition.md)",
            "[アーキテクチャ](docs-dev/architecture.md)",
            "[Excel / formula 契約](docs-dev/excel-contract.md)",
            "[Custom evaluator ガイド](docs-dev/custom-evaluator-guide.md)",
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
            "P-01完了時点（D-01文書テスト追加前）",
            "443件すべて成功",
            "3回の記録は中央値",
            "3.279264 秒",
            "AI待機とfixture生成を除外",
            "保証値ではありません",
            "optional live Copilot smoke | `NOT_RUN`",
            "optional external recalculation smoke | `NOT_RUN`",
            "実画面を取得していないため掲載していません");

        Assert.DoesNotContain("ZIP は commit 済み", readme, StringComparison.OrdinalIgnoreCase);
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
            "immutable snapshot",
            "canonical JSON",
            "SHA-256",
            "submit_quantification",
            "target-local",
            "no-overwrite atomic rename",
            "app-owned database と cloud backend はありません",
            "optional live Copilot smoke は `NOT_RUN`",
            "optional external spreadsheet recalculation smoke も `NOT_RUN`");
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
            "現在の状態は `NOT_RUN`");
    }

    [Fact]
    public void Custom_guide_has_exactly_six_placeholders_and_safe_rendering_contract()
    {
        string guide = Read("docs-dev/custom-evaluator-guide.md");
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
            "倫理gate");
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
            "docs-dev/custom-evaluator-guide.md",
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
            D01DocumentPaths.Select(Read));

        AssertDoesNotContainAny(
            allDocuments,
            "署名済み ZIP",
            "署名済みパッケージ",
            "デジタル署名済み",
            "Signing: SIGNED",
            "macOS 対応",
            "Linux 対応",
            "Windows Arm64 対応",
            "live Copilot smoke | `PASS`",
            "external recalculation smoke | `PASS`",
            "教育的妥当性を保証します",
            "教育的妥当性は保証済み",
            "公平性を保証します",
            "公平性は保証済み",
            "法的適合性を保証します",
            "法的適合性は保証済み",
            "品質を保証します",
            "品質は保証済み",
            "![",
            ".png",
            ".jpg",
            ".jpeg");
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
