using System.Reflection;
using System.Text.RegularExpressions;
using StudyReportEvaluator.App.Copilot;
using StudyReportEvaluator.App.Launch;
using StudyReportEvaluator.App.Resources;
using StudyReportEvaluator.App.Workbooks.Checkpoint;
using StudyReportEvaluator.App.Workbooks.Intake;
using StudyReportEvaluator.App.Workbooks.Writing;
using Xunit;

namespace StudyReportEvaluator.App.Tests.Content;

public sealed class DocumentationContractTests
{
    private static readonly string[] PublicDocumentPaths =
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
    public void Public_documents_exist_are_nonempty_and_have_no_broken_local_links()
    {
        string root = FindRepositoryRoot();
        int linkCount = 0;
        foreach (string relativePath in PublicDocumentPaths)
        {
            string path = Resolve(root, relativePath);
            Assert.True(File.Exists(path), $"Missing public document: {relativePath}");
            string content = File.ReadAllText(path);
            Assert.False(string.IsNullOrWhiteSpace(content));

            foreach (Match match in Regex.Matches(
                         content,
                         @"!?\[[^\]]*\]\((?<target>[^)]+)\)",
                         RegexOptions.CultureInvariant))
            {
                string target = match.Groups["target"].Value.Trim().Trim('<', '>');
                if (target.StartsWith('#')
                    || target.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                    || target.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                    || target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                linkCount++;
                target = target.Split('#', 2)[0].Split('?', 2)[0];
                target = Uri.UnescapeDataString(target);
                string resolved = Path.GetFullPath(
                    target.Replace('/', Path.DirectorySeparatorChar),
                    Path.GetDirectoryName(path)!);
                Assert.True(
                    IsWithinRoot(root, resolved),
                    $"Public link escapes the repository: {relativePath} -> {target}");
                Assert.True(
                    File.Exists(resolved) || Directory.Exists(resolved),
                    $"Broken public link: {relativePath} -> {target}");
            }
        }

        Assert.True(linkCount >= 25, $"Expected at least 25 local links, found {linkCount}.");
    }

    [Fact]
    public void Developer_documents_and_product_version_management_are_current()
    {
        string root = FindRepositoryRoot();
        string developerRoot = Resolve(root, "dev/docs");
        Assert.True(Directory.Exists(developerRoot));
        Assert.False(Directory.Exists(Resolve(root, "docs" + "-dev")));

        string[] requiredPaths =
        [
            "CHANGELOG.md",
            "dev/README.md",
            "dev/version.ps1",
            "dev/version.tests.ps1",
            "dev/docs/README.md",
            "dev/docs/version-management.md",
            "dev/docs/adr/0014-product-versioning.md",
            "dev/docs/adr/0016-windows-one-action-startup.md",
            "dev/docs/preflight/windows-singlefile-feasibility.md",
            "src/StudyReportEvaluator.App/Properties/PublishProfiles/WindowsSingleFile.pubxml",
            "scripts/package-windows-singlefile.ps1",
            "scripts/test-windows-singlefile.ps1",
        ];
        foreach (string relativePath in requiredPaths)
        {
            string path = Resolve(root, relativePath);
            Assert.True(File.Exists(path), $"Missing developer version-management artifact: {relativePath}");
            Assert.False(string.IsNullOrWhiteSpace(File.ReadAllText(path)));
        }

        string[] developerDocuments = Directory.GetFiles(
            developerRoot,
            "*.md",
            SearchOption.AllDirectories);
        Assert.True(
            developerDocuments.Length >= 32,
            $"Expected at least 32 developer documents after migration, found {developerDocuments.Length}.");
        int linkCount = 0;
        foreach (string path in developerDocuments)
        {
            string content = File.ReadAllText(path);
            Assert.DoesNotContain("docs" + "-dev", content, StringComparison.Ordinal);
            foreach (Match match in Regex.Matches(
                         content,
                         @"!?\[[^\]]*\]\((?<target>[^)]+)\)",
                         RegexOptions.CultureInvariant))
            {
                string target = match.Groups["target"].Value.Trim().Trim('<', '>');
                if (target.StartsWith('#')
                    || target.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                    || target.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                    || target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                linkCount++;
                target = Uri.UnescapeDataString(target.Split('#', 2)[0].Split('?', 2)[0]);
                string resolved = Path.GetFullPath(
                    target.Replace('/', Path.DirectorySeparatorChar),
                    Path.GetDirectoryName(path)!);
                Assert.True(
                    IsWithinRoot(root, resolved),
                    $"Developer document link escapes the repository: {Path.GetRelativePath(root, path)} -> {target}");
                Assert.True(
                    File.Exists(resolved) || Directory.Exists(resolved),
                    $"Broken developer document link: {Path.GetRelativePath(root, path)} -> {target}");
            }
        }

        Assert.True(linkCount >= 100, $"Expected at least 100 developer-document local links, found {linkCount}.");
        AssertContainsAll(
            Read("dev/docs/README.md"),
            "[アプリケーション版管理手順](version-management.md)",
            "[ADR-0014](adr/0014-product-versioning.md)",
            "[ADR-0016](adr/0016-windows-one-action-startup.md)",
            "[Windows単一EXEの方式適合](preflight/windows-singlefile-feasibility.md)",
            "`PASS_DEVELOPMENT`",
            "CH-01〜06");
        AssertContainsAll(
            Read("dev/README.md"),
            "[Windows単一EXEの設計決定](docs/adr/0016-windows-one-action-startup.md)",
            "[`WindowsSingleFile.pubxml`](../src/StudyReportEvaluator.App/Properties/PublishProfiles/WindowsSingleFile.pubxml)",
            "[`package-windows-singlefile.ps1`](../scripts/package-windows-singlefile.ps1)",
            "[`test-windows-singlefile.ps1`](../scripts/test-windows-singlefile.ps1)");
        AssertContainsAll(
            Read("dev/version.ps1"),
            "#Requires -Version 7.0",
            "#Requires -PSEdition Core",
            "'show', 'set', 'bump', 'verify'",
            "Directory.Build.props must contain exactly one VersionPrefix and one VersionSuffix element");
        Assert.Contains("## [Unreleased]", Read("CHANGELOG.md"), StringComparison.Ordinal);

        string buildProperties = Read("Directory.Build.props");
        Match prefix = Regex.Match(
            buildProperties,
            @"<VersionPrefix>(?<value>[^<]+)</VersionPrefix>",
            RegexOptions.CultureInvariant);
        Match suffix = Regex.Match(
            buildProperties,
            @"<VersionSuffix>(?<value>[^<]*)</VersionSuffix>",
            RegexOptions.CultureInvariant);
        Assert.True(prefix.Success);
        Assert.True(suffix.Success);
        string productVersion = prefix.Groups["value"].Value;
        if (suffix.Groups["value"].Value.Length > 0)
        {
            productVersion += "-" + suffix.Groups["value"].Value;
        }

        Assert.Matches(
            new Regex(
                @"^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$",
                RegexOptions.CultureInvariant),
            productVersion);
    }

    [Fact]
    public void Root_readme_contains_no_development_progress_or_placeholder_text()
    {
        string readme = Read("README.md");
        string[] forbidden =
        [
            "GATE-ACCEPTANCE",
            "IMPL-GAP-",
            "IMPLEMENTATION_IN_PROGRESS",
            "U-03",
            "U-04",
            "HEAD ",
            "dev/docs/",
            "traceability",
            "sourceからbuild",
            "dotnet test",
            "TODO",
            "TBD",
            "example.com",
            "<URL>",
            "公開後にこの節へ追加",
        ];
        AssertDoesNotContainAny(readme, forbidden);
        Assert.DoesNotMatch(
            new Regex(@"\b[0-9a-f]{7,40}\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase),
            readme);
        Assert.DoesNotMatch(
            new Regex(@"\b\d+\s*/\s*\d+\s*(?:PASS|成功|passed)\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase),
            readme);
    }

    [Fact]
    public void Current_documents_link_to_existing_local_markdown_headings()
    {
        string root = FindRepositoryRoot();
        IEnumerable<string> paths = PublicDocumentPaths.Select(path => Resolve(root, path))
            .Concat(Directory.EnumerateFiles(Resolve(root, "dev/docs"), "*.md"))
            .Distinct();
        List<string> broken = [];
        foreach (string path in paths)
        {
            foreach (Match match in Regex.Matches(File.ReadAllText(path), @"!?\[[^\]]*\]\((?<target>[^)]+)\)"))
            {
                string target = match.Groups["target"].Value.Trim().Trim('<', '>');
                if (Regex.IsMatch(target, @"^[a-z]+:", RegexOptions.IgnoreCase))
                {
                    continue;
                }

                string[] parts = target.Split('#', 2);
                if (parts.Length != 2 || parts[1].Length == 0)
                {
                    continue;
                }

                string destination = parts[0].Length == 0 ? path : Path.GetFullPath(
                    Uri.UnescapeDataString(parts[0]), Path.GetDirectoryName(path)!);
                if (Path.GetExtension(destination).Equals(".md", StringComparison.OrdinalIgnoreCase)
                    && (!File.Exists(destination) || !MarkdownAnchors(File.ReadAllText(destination))
                        .Contains(Uri.UnescapeDataString(parts[1]))))
                {
                    broken.Add($"{Path.GetRelativePath(root, path)} -> {target}");
                }
            }
        }

        Assert.True(broken.Count == 0, string.Join(Environment.NewLine, broken));
    }

    [Fact]
    public void Heading_anchor_check_ignores_code_and_detects_missing_targets()
    {
        HashSet<string> anchors = MarkdownAnchors("## Evidence integrity and storage\n```text\n## Not a heading\n```\n### 主なvalidation\n");
        Assert.Contains("evidence-integrity-and-storage", anchors);
        Assert.Contains("主なvalidation", anchors);
        Assert.DoesNotContain("not-a-heading", anchors);
        Assert.DoesNotContain("missing", anchors);
    }

    [Fact]
    public void Ethics_warning_is_exact_in_readme_and_user_guides()
    {
        foreach (string path in new[]
                 {
                     "README.md",
                     "docs/README.md",
                     "docs/getting-started.md",
                     "docs/custom-evaluator-guide.md",
                     "docs/privacy-and-data-handling.md",
                 })
        {
            Assert.Contains(EthicsWarningText.Message, Read(path), StringComparison.Ordinal);
        }

        Assert.Single(
            typeof(EthicsWarningText).GetFields(BindingFlags.Public | BindingFlags.Static));
    }

    [Fact]
    public void Platform_and_published_package_claims_match_the_windows_delivery_contract()
    {
        string readme = Read("README.md");
        string userIndex = Read("docs/README.md");
        string publishScript = Read("scripts/publish-windows.ps1");
        string packageScript = Read("scripts/package-windows.ps1");

        AssertContainsAll(
            readme,
            "Windows 11 x64",
            ".NET 10 self-contained",
            "公開`v0.8.1`はunsigned ZIP",
            "StudyReportEvaluator-win-x64.zip",
            "StudyReportEvaluator-win-x64.zip.sha256",
            "StudyReportEvaluator-win-x64.exe",
            "StudyReportEvaluator-win-x64.exe.sha256",
            "現在の公開版は`0.8.1`です",
            "https://github.com/dahatake/StudyReport-Evaluator/releases",
            "clean-host試験と本人loginは`NOT_RUN`",
            "macOS、Linux、Windows Arm64は初版対応対象外",
            "installer、code signing、notarizationを提供しません");

        // A versioned direct link goes stale on the next release and once returned 404 to users.
        Assert.DoesNotContain("/releases/download/", readme, StringComparison.OrdinalIgnoreCase);
        AssertContainsAll(
            userIndex,
            "Windows 11 x64",
            "公開ZIP／候補EXEともunsigned",
            "現在の公開版は`0.8.1`です",
            "単一EXEはまだ公開されておらず",
            "clean-host試験と本人loginは`NOT_RUN`",
            "development MSIXは検証専用で、一般配布しません",
            "macOS、Linux、Windows Arm64");
        AssertContainsAll(
            Read("docs/getting-started.md"),
            "現在の公開版は`v0.8.1`です",
            "https://github.com/dahatake/StudyReport-Evaluator/releases",
            "fresh Windowsのclean-host試験CH-01〜06と本人loginは`NOT_RUN`",
            "development MSIXは非公開の開発検証専用");
        AssertContainsAll(
            publishScript,
            "$RuntimeIdentifier = 'win-x64'",
            "--self-contained",
            "[switch] $SingleFile",
            "Assert-BundledCopilotRuntime");
        AssertContainsAll(
            packageScript,
            "Signing: UNSIGNED",
            "GitHub Copilot CLI runtime: BUNDLED");
    }

    [Fact]
    public void Bundled_cli_documentation_matches_the_default_resolver_and_never_advises_path_fallback()
    {
        string publicContent = string.Join(Environment.NewLine, PublicDocumentPaths.Select(Read));
        AssertContainsAll(
            publicContent,
            "配布物へ同梱したGitHub Copilot CLI",
            "PATH上の別CLIへfallbackしません",
            "runtimes\\win-x64\\native\\copilot.exe");
        AssertDoesNotContainAny(
            publicContent,
            "Copilot CLIを別途導入",
            "PATHから`copilot.exe`を解決",
            "PATH上のCLIを使います");

        CopilotClientFactory factory = new();
        FieldInfo resolverField = typeof(CopilotClientFactory).GetField(
            "_pathResolver",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Copilot path resolver field not found.");
        Assert.IsType<BundledCopilotCliPathResolver>(resolverField.GetValue(factory));
    }

    [Fact]
    public void Input_format_claims_match_the_closed_classifier_vocabulary()
    {
        string content = Read("README.md") + Environment.NewLine + Read("docs/troubleshooting.md");
        AssertContainsAll(
            content,
            "標準Office Open XML `.xlsx`",
            ".xls",
            ".xlsb",
            "CSV",
            "PDF",
            "MacroEnabledWorkbook",
            "EncryptedOrRightsProtected",
            "UnsafePackage",
            "InvalidRelationship");
        Assert.Equal(FileFormatClassification.StandardXlsx, Enum.GetValues<FileFormatClassification>()[0]);
        Assert.Equal(14, Enum.GetValues<FileFormatClassification>().Length);
    }

    [Fact]
    public void Scoring_output_and_checkpoint_names_match_production_constants()
    {
        string readme = Read("README.md");
        string features = Read("docs/features.md");
        foreach (string name in new[]
                 {
                     AppOwnedSheetNameResolver.ConfigBaseName,
                     AppOwnedSheetNameResolver.ReferencesBaseName,
                     AppOwnedSheetNameResolver.ResultsBaseName,
                     AppOwnedSheetNameResolver.RunBaseName,
                     CheckpointStore.CheckpointSheetName,
                 })
        {
            Assert.Contains(name, readme, StringComparison.Ordinal);
        }

        AssertContainsAll(
            readme,
            "BasePoints + SpecialPoints + \\sum_{q=1}^{N}QuestionPoints_q = 100",
            "QuestionEarned_q=QuestionPoints_q\\times QuestionRate_q",
            "SimilarityPenalty_q=QuestionPoints_q\\times Similarity_q\\times SimilarityPenaltyWeight",
            "eval-yyyyMMdd-HHmm[-NN].xlsx",
            "eval-yyyyMMdd-HHmm[-NN].partial.xlsx",
            "空の主回答: AI callなし",
            "技術的AI失敗: 対象値はblank");
        AssertContainsAll(
            features,
            "各Reference完了後",
            "1 student row",
            "Final rawは監査用",
            "Final scoreを0〜100");
    }

    [Fact]
    public void Launch_documentation_matches_parser_utf8_and_no_auto_run_contract()
    {
        string readme = Read("README.md");
        string guide = Read("docs/prompt-launch.md");
        AssertContainsAll(
            readme,
            "StudyReportEvaluator.App.exe --input <xlsx-path> --prompt <txt-path> [--prompt <txt-path> ...]",
            "StudyReportEvaluator-win-x64.exe --input <xlsx-path> --prompt <txt-path> [--prompt <txt-path> ...]",
            "`--input`は0または1回",
            "`--prompt`は0回以上",
            "strict UTF-8",
            "起動引数やPrompt適用ではloginもAI処理も自動開始しません",
            "AI処理はExecution画面の明示操作まで開始しません");
        AssertContainsAll(
            guide,
            "BOMあり／なしを受理",
            "1〜32,767 UTF-16 code units",
            "`--run`や`--resume`はありません",
            "filenameによる自動割当は行いません");
        Assert.Equal(32_767, PromptFileLoader.MaximumCharacters);
        Assert.NotNull(LaunchOptions.Parse([]));
    }

    [Fact]
    public void Privacy_documentation_distinguishes_payload_log_and_output_boundaries()
    {
        string readme = Read("README.md");
        string privacy = Read("docs/privacy-and-data-handling.md");
        AssertContainsAll(
            readme,
            "current rowの選択済みprimary／supporting／special sourceだけ",
            "他row、非選択列、workbook pathは通常payloadへ含めません",
            "application logは回答、Prompt、Reference、reason、evidence、credentialを受け取るfree-text surfaceを持ちません",
            "入力と同等以上に機密");
        AssertContainsAll(
            privacy,
            "| Reference | なし |",
            "| Normal | current rowの選択済み主回答・補助列 |",
            "| Special | current rowの選択済み固有評価主値・補助列 |",
            "| Similarity | current rowの主回答 |",
            "shell、filesystem、Web、GitHub write、MCP toolを公開しません",
            "partialは暗号化containerではありません");
    }

    [Fact]
    public void Screenshot_captions_and_manifest_disclose_synthetic_and_fake_state()
    {
        string readme = Read("README.md");
        string manifest = Read("images/README.md");
        AssertContainsAll(
            readme,
            "synthetic dataを使用",
            "fake scoreを使用");
        AssertContainsAll(
            manifest,
            "fake authentication boundary",
            "fake row/AI/checkpoint/output boundaries",
            "実fileを作成した証跡ではない",
            "personal/student data: なし");

        foreach (string fileName in new[]
                 {
                     "01-input-workbook.png",
                     "02-input-mapping.png",
                     "03-design-knowledge.png",
                     "04-design-custom-prompt.png",
                     "05-execution-auto.png",
                     "06-results-review.png",
                     "07-output-export.png",
                 })
        {
            Assert.True(File.Exists(Resolve(FindRepositoryRoot(), "images/" + fileName)));
            Assert.Contains(fileName, manifest, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Unreleased_ui_and_screenshots_are_explicitly_distinguished_from_the_public_release()
    {
        foreach (string path in new[]
                 {
                     "README.md",
                     "docs/README.md",
                     "docs/getting-started.md",
                     "docs/features.md",
                     "images/README.md",
                 })
        {
            AssertContainsAll(Read(path), "UNRELEASED", "`0.8.4`候補", "`0.8.1`");
        }

        AssertContainsAll(Read("images/README.md"), "一時directoryへ描画", "2回生成の一致", "finally");
        AssertContainsAll(
            Read("docs/getting-started.md"),
            "確認のためだけにPowerShell等を導入する必要はありません",
            "certutil",
            "fresh Windowsのclean-host試験CH-01〜06と本人loginは`NOT_RUN`");
    }

    [Fact]
    public void Readme_excludes_unsupported_guarantees_and_links_the_mit_license()
    {
        string readme = Read("README.md");
        AssertContainsAll(
            readme,
            "AI品質、教育的妥当性、公平性、法的適合性、組織policy適合性、不正行為を保証・判定しません",
            "未実測の処理時間、token数、費用を保証しません",
            "[MIT License](LICENSE)");
        AssertDoesNotContainAny(
            readme,
            "自動採点します",
            "不正検知",
            "公平性を保証",
            "signed installer",
            "macOS対応");
        Assert.StartsWith("MIT License", Read("LICENSE"), StringComparison.Ordinal);
    }

    [Fact]
    public void Requirements_claim_ledger_and_system_prompts_are_complete_and_current()
    {
        string root = FindRepositoryRoot();
        string requirements = Read("docs/requirements-definition.md");
        string ledger = Read("dev/docs/readme-claim-ledger.md");
        string sampleProfile = Read("dev/docs/preflight/sample-workbook-profile.md");
        string prompts = Read("SystemTest-prompt.md");
        string realDataSmoke = Read("tests/StudyReportEvaluator.App.Tests/E2E/RealDataSystemSmokeTests.cs");

        Assert.False(
            File.Exists(Resolve(root, "tests/system-test-prompt.md")),
            "The consolidated root SystemTest-prompt.md must be the only system-test Prompt source.");
        Assert.False(
            File.Exists(Resolve(root, "tests/e2e-systemtest-prompt.md")),
            "The legacy real-data Prompt must remain consolidated into root SystemTest-prompt.md.");

        AssertContainsAll(
            requirements,
            "| 文書版 | 4.5 |",
            "| 基準日 | 2026-09-06 |",
            "ADR-0013",
            "ADR-0016",
            "| 対応環境 | Windows 11 x64。macOS、Linux、Windows Arm64は現版の正式公開対象外 |",
            "StudyReportEvaluator-win-x64.exe",
            "CH-01〜06",
            "`sample/SampleReport.xlsx`",
            "| Bytes | 470,806 |",
            "73883CE3BBB86B93AF8825C04F596434CF82A2C6309A7F4CC5835AE8F3E542EA",
            "| package entries / relationships | 11 / 8 |",
            "同directoryの他fileを列挙、fallback、代用しない",
            "利用者が主回答列を選択した場合",
            "交差セルが空または存在しない場合は質問文を空として扱い",
            "`HEADER_METADATA_MISMATCH`で再読込を要求");
        AssertDoesNotContainAny(
            requirements,
            "sample/realdata.xlsx",
            "469,995",
            "F7C5364449B1026F2725828F47418B8E105D7E50CF4DF0B224FE4EAF134A2E3D",
            "A1:J531");
        AssertContainsAll(
            sampleProfile,
            "| Requirement | `docs/requirements-definition.md` v4.3 |",
            "| Verification date | 2026-09-04 |",
            "| Sample | `sample/SampleReport.xlsx` |",
            "| Bytes | 470,806 |",
            "73883CE3BBB86B93AF8825C04F596434CF82A2C6309A7F4CC5835AE8F3E542EA",
            "| ZIP entries | 11 |",
            "| Package relationships | 8 |",
            "同directoryの他fileを列挙、fallback、代用しない");
        AssertDoesNotContainAny(
            sampleProfile,
            "sample/realdata.xlsx",
            "469,995",
            "F7C5364449B1026F2725828F47418B8E105D7E50CF4DF0B224FE4EAF134A2E3D",
            "A1:J531");
        AssertSequentialTableIds(requirements, "AC-", 34);
        AssertSequentialTableIds(ledger, "C-", 44);
        AssertContainsAll(ledger, "VERIFIED", "BLOCKED", "EXCLUDED");
        AssertContainsAll(
            ledger,
            "`docs/requirements-definition.md` v4.5",
            "`SystemTest-prompt.md` v4.5、ST-UC-01〜26、TR-01〜33",
            "`PASS_DEVELOPMENT`",
            "fresh user本人login／再起動後確認のCH-06は`NOT_RUN`");

        int[] scenarioHeadingIds = Regex.Matches(
                prompts,
                @"^### ST-UC-(\d{2}):",
                RegexOptions.Multiline | RegexOptions.CultureInvariant)
            .Select(match => int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();
        Assert.Equal(Enumerable.Range(1, 26), scenarioHeadingIds);

        Match[] standalonePrompts = Regex.Matches(
                prompts,
                @"^Test ID: ST-UC-(\d{2})\r?\nRequirement: (?<requirement>[^\r\n]+)\r?$",
                RegexOptions.Multiline | RegexOptions.CultureInvariant)
            .Cast<Match>()
            .ToArray();
        Assert.Equal(
            Enumerable.Range(1, 26),
            standalonePrompts.Select(match => int.Parse(
                match.Groups[1].Value,
                System.Globalization.CultureInfo.InvariantCulture)));

        string[] expectedRequirementMappings =
        [
            "TR-01 / AC-001 / AC-002",
            "TR-02 / AC-003 / docs/requirements-definition.md §4.4",
            "TR-03 / AC-001 / AC-002",
            "TR-04 / TR-05 / AC-004 / AC-005 / AC-006 / AC-008",
            "TR-06 / AC-007 / AC-008",
            "TR-07 / TR-08 / AC-009 / AC-010 / AC-012",
            "TR-09 / TR-10 / AC-006 / AC-011 / AC-012",
            "TR-11 / TR-12 / AC-013",
            "TR-13 / TR-14 / AC-014 / AC-015",
            "TR-15 / AC-012 / AC-016",
            "TR-16 / AC-019",
            "TR-17 / AC-002 / AC-016 / AC-017",
            "TR-18 / AC-018",
            "TR-19 / AC-020",
            "TR-20 / AC-020",
            "TR-21 / TR-22 / AC-021 / AC-022",
            "supplemental deterministic scenario / AC-009〜AC-016 / AC-019",
            "TR-23 / AC-009〜AC-016 / AC-019",
            "real-data technical E2E / AC-009〜AC-016 / AC-019",
            "TR-24A advisory",
            "TR-24B advisory",
            "TR-25 / AC-023 / AC-027",
            "TR-26 / AC-024",
            "TR-27 / AC-025",
            "TR-28 / TR-29 / AC-026 / AC-027 / AC-028",
            "TR-30 / TR-31 / TR-32 / TR-33 / AC-029 / AC-030 / AC-031 / AC-032 / AC-033 / AC-034",
        ];
        Assert.Equal(
            expectedRequirementMappings,
            standalonePrompts.Select(match => match.Groups["requirement"].Value));

        int[] coveredRequirementIds = Regex.Matches(
                string.Join(
                    Environment.NewLine,
                    standalonePrompts.Select(match => match.Groups["requirement"].Value)),
                @"\bTR-(\d{2})(?:A|B)?\b",
                RegexOptions.CultureInvariant)
            .Select(match => int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture))
            .Distinct()
            .Order()
            .ToArray();
        Assert.Equal(Enumerable.Range(1, 33), coveredRequirementIds);
        AssertContainsAll(
            prompts,
            "| 対象 | StudyReport Evaluator v4.5 |",
            "| 基準日 | 2026-09-06 |",
            "| 要求正本 | `docs/requirements-definition.md` v4.5 |",
            "ST-UC-26",
            "CH-01〜CH-06",
            "`sample/SampleReport.xlsx`",
            "73883CE3BBB86B93AF8825C04F596434CF82A2C6309A7F4CC5835AE8F3E542EA",
            "他fileを列挙、fallback、代用しない",
            "question row 1と2の各fixtureで主回答列を変更すると",
            "Inputの主回答列ComboBoxを操作すると");
        Assert.DoesNotContain("sample/realdata.xlsx", prompts, StringComparison.OrdinalIgnoreCase);
        AssertContainsAll(
            realDataSmoke,
            "requirements = \"docs/requirements-definition.md v4.5\"",
            "system_test_prompt = \"SystemTest-prompt.md v4.5\"");
        AssertContainsAll(
            Read("docs/getting-started.md"),
            "主回答列を選択すると",
            "設問text**へ即座に表示",
            "**見出し行を再読込**");
        AssertContainsAll(
            Read("docs/features.md"),
            "設問textへそのまま即座に反映",
            "空または存在しないheader cellには代替文を生成しません",
            "同一Questionの主回答列と補助列に同じ列を重複指定することはできません");
    }

    [Fact]
    public void Package_script_includes_every_public_document_image_and_license()
    {
        string packageScript = Read("scripts/package-windows.ps1");
        AssertContainsAll(
            packageScript,
            "$documentationRelativePaths = @(",
            "'README.md'",
            "'LICENSE'",
            "'docs\\getting-started.md'",
            "'images\\07-output-export.png'",
            "Documentation package input must be LICENSE, Markdown, or PNG and nonempty");

        string packageTest = Read("tests/StudyReportEvaluator.App.Tests/Packaging/WindowsPublishPackageTests.cs");
        foreach (string required in new[]
                 {
                     "LICENSE",
                     "README.md",
                     "docs/getting-started.md",
                     "docs/features.md",
                     "docs/custom-evaluator-guide.md",
                     "docs/prompt-launch.md",
                     "docs/privacy-and-data-handling.md",
                     "docs/troubleshooting.md",
                     "images/README.md",
                     "images/01-input-workbook.png",
                     "images/02-input-mapping.png",
                     "images/03-design-knowledge.png",
                     "images/04-design-custom-prompt.png",
                     "images/05-execution-auto.png",
                     "images/06-results-review.png",
                     "images/07-output-export.png",
                 })
        {
            Assert.Contains($"\"{required}\"", packageTest, StringComparison.Ordinal);
        }
    }

    private static void AssertSequentialTableIds(string content, string prefix, int expectedCount)
    {
        int[] actual = Regex.Matches(
                content,
                $@"^\| {Regex.Escape(prefix)}(\d{{3}}) \|",
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

    private static HashSet<string> MarkdownAnchors(string content)
    {
        HashSet<string> anchors = new(StringComparer.Ordinal);
        Dictionary<string, int> counts = new(StringComparer.Ordinal);
        bool fenced = false;
        foreach (string line in content.Split('\n'))
        {
            string text = line.Trim();
            if (text.StartsWith("```", StringComparison.Ordinal) || text.StartsWith("~~~", StringComparison.Ordinal))
            {
                fenced = !fenced;
                continue;
            }

            Match heading = Regex.Match(text, @"^#{1,6}\s+(?<title>.+?)\s*#*$");
            if (fenced || !heading.Success)
            {
                continue;
            }

            string slug = Regex.Replace(heading.Groups["title"].Value.ToLowerInvariant(), @"[^\p{L}\p{M}\p{N}_\- ]", string.Empty)
                .Replace(' ', '-');
            int count = counts.GetValueOrDefault(slug);
            counts[slug] = count + 1;
            anchors.Add(count == 0 ? slug : $"{slug}-{count}");
        }

        return anchors;
    }

    private static string Resolve(string root, string relativePath) =>
        Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));

    private static bool IsWithinRoot(string root, string path)
    {
        string canonicalRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        string canonicalPath = Path.GetFullPath(path);
        return canonicalPath.StartsWith(
            canonicalRoot,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

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
