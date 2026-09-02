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
            "[ADR-0014](adr/0014-product-versioning.md)");
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
    public void Platform_and_unsigned_package_claims_match_the_windows_delivery_contract()
    {
        string readme = Read("README.md");
        string userIndex = Read("docs/README.md");
        string publishScript = Read("scripts/publish-windows.ps1");
        string packageScript = Read("scripts/package-windows.ps1");

        AssertContainsAll(
            readme,
            "Windows 11 x64",
            ".NET 10 self-contained",
            "unsigned ZIP",
            "StudyReportEvaluator-win-x64.zip",
            "StudyReportEvaluator-win-x64.zip.sha256",
            "https://github.com/dahatake/StudyReport-Evaluator/releases/download/v1.0.0/StudyReportEvaluator-win-x64.zip",
            "https://github.com/dahatake/StudyReport-Evaluator/releases/download/v1.0.0/StudyReportEvaluator-win-x64.zip.sha256",
            "macOS、Linux、Windows Arm64は初版対応対象外",
            "installer、code signing、notarizationを提供しません");
        AssertContainsAll(
            userIndex,
            "Windows 11 x64",
            "unsigned ZIP",
            "macOS、Linux、Windows Arm64");
        AssertContainsAll(
            publishScript,
            "$RuntimeIdentifier = 'win-x64'",
            "--self-contained",
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
            "ZIPへ同梱したGitHub Copilot CLI",
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
            "`--input`は0または1回",
            "`--prompt`は0回以上",
            "strict UTF-8",
            "AI処理はExecution画面の明示操作まで開始しない");
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

        Assert.False(
            File.Exists(Resolve(root, "tests/system-test-prompt.md")),
            "The consolidated root SystemTest-prompt.md must be the only system-test Prompt source.");
        Assert.False(
            File.Exists(Resolve(root, "tests/e2e-systemtest-prompt.md")),
            "The legacy real-data Prompt must remain consolidated into root SystemTest-prompt.md.");

        AssertContainsAll(
            requirements,
            "| 文書版 | 4.1 |",
            "| 基準日 | 2026-09-03 |",
            "ADR-0013",
            "| 対応環境 | Windows 11 x64 |",
            "`sample/SampleReport.xlsx`",
            "| Bytes | 470,806 |",
            "73883CE3BBB86B93AF8825C04F596434CF82A2C6309A7F4CC5835AE8F3E542EA",
            "| package entries / relationships | 11 / 8 |",
            "同directoryの他fileを列挙、fallback、代用しない");
        AssertDoesNotContainAny(
            requirements,
            "sample/realdata.xlsx",
            "469,995",
            "F7C5364449B1026F2725828F47418B8E105D7E50CF4DF0B224FE4EAF134A2E3D",
            "A1:J531");
        AssertContainsAll(
            sampleProfile,
            "| Verification date | 2026-09-03 |",
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
        AssertSequentialTableIds(requirements, "AC-", 22);
        AssertSequentialTableIds(ledger, "C-", 32);
        AssertContainsAll(ledger, "VERIFIED", "BLOCKED", "EXCLUDED");

        int[] scenarioHeadingIds = Regex.Matches(
                prompts,
                @"^### ST-UC-(\d{2}):",
                RegexOptions.Multiline | RegexOptions.CultureInvariant)
            .Select(match => int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();
        Assert.Equal(Enumerable.Range(1, 21), scenarioHeadingIds);

        Match[] standalonePrompts = Regex.Matches(
                prompts,
                @"^Test ID: ST-UC-(\d{2})\r?\nRequirement: (?<requirement>[^\r\n]+)\r?$",
                RegexOptions.Multiline | RegexOptions.CultureInvariant)
            .Cast<Match>()
            .ToArray();
        Assert.Equal(
            Enumerable.Range(1, 21),
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
            "TR-17 / AC-016 / AC-017",
            "TR-18 / AC-018",
            "TR-19 / AC-020",
            "TR-20 / AC-020",
            "TR-21 / TR-22 / AC-021 / AC-022",
            "supplemental deterministic scenario / AC-009〜AC-016 / AC-019",
            "TR-23 / AC-009〜AC-016 / AC-019",
            "real-data technical E2E / AC-009〜AC-016 / AC-019",
            "TR-24A advisory",
            "TR-24B advisory",
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
        Assert.Equal(Enumerable.Range(1, 24), coveredRequirementIds);
        AssertContainsAll(
            prompts,
            "| 対象 | StudyReport Evaluator v4.1 |",
            "| 基準日 | 2026-09-03 |",
            "| 要求正本 | `docs/requirements-definition.md` v4.1 |",
            "`sample/SampleReport.xlsx`",
            "73883CE3BBB86B93AF8825C04F596434CF82A2C6309A7F4CC5835AE8F3E542EA",
            "他fileを列挙、fallback、代用しない");
        Assert.DoesNotContain("sample/realdata.xlsx", prompts, StringComparison.OrdinalIgnoreCase);
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
