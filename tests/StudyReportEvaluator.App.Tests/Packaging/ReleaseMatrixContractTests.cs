using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace StudyReportEvaluator.App.Tests.Packaging;

public sealed class ReleaseMatrixContractTests
{
    [Fact]
    public async Task Valid_matrix_binds_the_exact_initial_public_asset_set()
    {
        using MatrixFixture fixture = new();

        ProcessResult result = await fixture.ValidateAsync(
            TestContext.Current.CancellationToken);

        AssertProcessSucceeded(result);
        using JsonDocument output = JsonDocument.Parse(result.StandardOutput);
        Assert.Equal("PASS", output.RootElement.GetProperty("status").GetString());
        Assert.Equal(2, output.RootElement.GetProperty("rowCount").GetInt32());
        Assert.Equal(
            [
                MatrixFixture.ZipFileName,
                MatrixFixture.ZipSidecarFileName,
            ],
            output.RootElement.GetProperty("publishableAssets")
                .EnumerateArray()
                .Select(item => item.GetString()
                    ?? throw new InvalidDataException("Publishable asset name is null."))
                .ToArray());
    }

    [Fact]
    public void Legacy_v1_reader_does_not_require_the_PowerShell_75_DateKind_parameter()
    {
        string script = File.ReadAllText(Path.Combine(
            MatrixFixture.FindRepositoryRoot(),
            "scripts",
            "validate-platform-release-matrix.ps1"));
        int readerStart = script.IndexOf("function Read-StrictJsonObject", StringComparison.Ordinal);
        int nextFunction = script.IndexOf("function Resolve-ReferencedFile", readerStart, StringComparison.Ordinal);

        Assert.True(readerStart >= 0 && nextFunction > readerStart);
        string reader = script[readerStart..nextFunction];
        string executableReader = string.Join(
            '\n',
            reader.Split('\n').Where(line => !line.TrimStart().StartsWith('#')));
        Assert.Contains("ConvertFrom-LegacyJsonElement", reader, StringComparison.Ordinal);
        Assert.DoesNotContain("-DateKind", executableReader, StringComparison.Ordinal);
        Assert.Contains("#Requires -Version 7.4", script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unknown_property_is_rejected_by_the_closed_schema()
    {
        using MatrixFixture fixture = new();
        fixture.Matrix["unexpected"] = true;
        fixture.SaveMatrix();

        await AssertProcessFailedAsync(
            fixture,
            "Release matrix schema validation failed.");
    }

    [Fact]
    public async Task Duplicate_artifact_kind_is_rejected()
    {
        using MatrixFixture fixture = new();
        JsonArray rows = fixture.Rows;
        rows[1] = rows[0]!.DeepClone();
        fixture.SaveMatrix();

        await AssertProcessFailedAsync(
            fixture,
            "Release matrix schema validation failed.");
    }

    [Fact]
    public async Task Missing_required_row_is_rejected()
    {
        using MatrixFixture fixture = new();
        fixture.Rows.RemoveAt(1);
        fixture.SaveMatrix();

        await AssertProcessFailedAsync(
            fixture,
            "Release matrix schema validation failed.");
    }

    [Fact]
    public async Task Non_pass_required_publish_row_is_rejected()
    {
        using MatrixFixture fixture = new();
        fixture.ZipRow["status"] = "FAIL";
        fixture.SaveMatrix();

        await AssertProcessFailedAsync(
            fixture,
            "Release matrix schema validation failed.");
    }

    [Fact]
    public async Task Artifact_hash_mismatch_is_rejected()
    {
        using MatrixFixture fixture = new();
        fixture.ZipRow["artifact"]!.AsObject()["sha256"] = new string('0', 64);
        fixture.SaveMatrix();

        await AssertProcessFailedAsync(
            fixture,
            "windows-zip artifact SHA-256 does not match the matrix.");
    }

    [Fact]
    public async Task Sidecar_content_mismatch_is_rejected_even_when_its_descriptor_matches()
    {
        using MatrixFixture fixture = new();
        File.WriteAllText(
            fixture.Resolve(MatrixFixture.ZipSidecarFileName),
            $"{fixture.ZipSha256}  {MatrixFixture.ZipFileName}\r\n",
            MatrixFixture.Utf8NoBom);
        fixture.ZipRow["sidecar"] = fixture.Descriptor(MatrixFixture.ZipSidecarFileName);
        fixture.SaveMatrix();

        await AssertProcessFailedAsync(
            fixture,
            "windows-zip sidecar content does not exactly match the artifact.");
    }

    [Fact]
    public async Task Wrong_public_asset_basename_is_rejected()
    {
        using MatrixFixture fixture = new();
        fixture.ZipRow["artifact"]!.AsObject()["fileName"] = "wrong.zip";
        fixture.SaveMatrix();

        await AssertProcessFailedAsync(
            fixture,
            "Release matrix schema validation failed.");
    }

    [Fact]
    public async Task Evidence_status_tamper_is_rejected_even_when_its_descriptor_matches()
    {
        using MatrixFixture fixture = new();
        fixture.ZipEvidence["status"] = "FAIL";
        fixture.WriteJson(MatrixFixture.ZipEvidenceFileName, fixture.ZipEvidence);
        fixture.ZipRow["evidence"] = fixture.Descriptor(MatrixFixture.ZipEvidenceFileName);
        fixture.SaveMatrix();

        await AssertProcessFailedAsync(
            fixture,
            "Windows ZIP evidence is not bound to the matrix, package, environment, and required checks.");
    }

    [Fact]
    public async Task Legacy_evidence_case_variant_property_is_rejected_by_the_closed_schema()
    {
        using MatrixFixture fixture = new();
        fixture.ZipEvidence["Status"] = "FAIL";
        fixture.WriteJson(MatrixFixture.ZipEvidenceFileName, fixture.ZipEvidence);
        fixture.ZipRow["evidence"] = fixture.Descriptor(MatrixFixture.ZipEvidenceFileName);
        fixture.SaveMatrix();

        await AssertProcessFailedAsync(
            fixture,
            "Windows ZIP evidence property set is not closed.");
    }

    [Fact]
    public async Task Legacy_evidence_Keys_property_cannot_shadow_the_real_dictionary_keys()
    {
        using MatrixFixture fixture = new();
        fixture.ZipEvidence["Keys"] = new JsonArray(
            "schemaVersion",
            "evidenceKind",
            "status",
            "sourceCommit",
            "sourceStatusEntryCount",
            "sourceStatusSha256",
            "productVersion",
            "host",
            "package",
            "checks");
        fixture.ZipEvidence["accessToken"] = "SYNTHETIC_CREDENTIAL_MUST_NOT_BE_PRINTED";
        fixture.WriteJson(MatrixFixture.ZipEvidenceFileName, fixture.ZipEvidence);
        fixture.ZipRow["evidence"] = fixture.Descriptor(MatrixFixture.ZipEvidenceFileName);
        fixture.SaveMatrix();

        await AssertProcessFailedAsync(
            fixture,
            "Windows ZIP evidence property set is not closed.");
    }

    // These are structural/byte-binding fixtures, not launch, PE/MSIX integrity,
    // trusted GitHub-run, personal authentication or clean-host execution proof.
    // Every invocation runs the real validator via its public pwsh CLI contract.
    [Fact]
    public async Task V2_final_matrix_binds_four_assets_without_msix_or_raw_records()
    {
        using V2Fixture fixture = new();
        File.Delete(fixture.Resolve(MatrixFixture.MsixFileName));
        Assert.False(File.Exists(fixture.Resolve("windows-singlefile.trx")));
        Assert.False(File.Exists(fixture.Resolve("CH-01.txt")));
        string[] before = fixture.Snapshot();

        ProcessResult result = await fixture.ValidateAsync(TestContext.Current.CancellationToken);

        AssertV2Succeeded(result, fixture, candidateOnly: false);
        Assert.Equal(before, fixture.Snapshot());
    }

    [Fact]
    public async Task V2_candidate_requires_packages_but_not_matrix_or_clean_host()
    {
        using V2Fixture fixture = new();
        File.Delete(fixture.Resolve("matrix.json"));
        File.Delete(fixture.Resolve(V2Fixture.CleanHostFileName));
        string[] before = fixture.Snapshot();

        ProcessResult result = await fixture.ValidateAsync(
            TestContext.Current.CancellationToken, candidateOnly: true);

        AssertV2Succeeded(result, fixture, candidateOnly: true);
        Assert.Equal(before, fixture.Snapshot());
    }

    [Fact]
    public async Task V2_candidate_rejects_a_missing_local_msix_binary()
    {
        using V2Fixture fixture = new();
        File.Delete(fixture.Resolve(MatrixFixture.MsixFileName));

        ProcessResult result = await fixture.ValidateAsync(
            TestContext.Current.CancellationToken, candidateOnly: true);

        AssertV2Failed(result, "C02_MISSING_FILE");
    }

    [Fact]
    public async Task V2_accepts_the_MSBuild_UTF8_BOM_in_the_bundled_manifest_only()
    {
        using V2Fixture fixture = new();
        fixture.ReplaceZip("bom-manifest");

        ProcessResult result = await fixture.ValidateAsync(TestContext.Current.CancellationToken);

        AssertV2Succeeded(result, fixture, candidateOnly: false);
    }

    [Theory]
    [InlineData("NOT_RUN")]
    [InlineData("FAIL")]
    public async Task V2_advisory_results_and_recorded_warning_actions_do_not_block_required_pass(string status)
    {
        using V2Fixture fixture = new();
        foreach (JsonNode? test in fixture.CleanHost["tests"]!.AsArray().Skip(6))
        {
            test!["status"] = status;
            test["operations"] = status == "NOT_RUN" ? 0 : 1;
            test["record"] = status == "NOT_RUN" ? null : V2Fixture.Observation("advisory.txt");
        }
        fixture.CleanHost["protection"]!["launchOutcome"] = "WarnedThenAllowed";
        fixture.CleanHost["protection"]!["warningActions"] = 2;
        // C01 permits fractional precision beyond DateTime's seven digits.
        fixture.CleanHost["measuredAtUtc"] = "2024-02-29T12:34:56.1234567890123456789Z";
        fixture.SaveCleanHost();
        fixture.SaveMatrix();

        ProcessResult result = await fixture.ValidateAsync(TestContext.Current.CancellationToken);

        AssertV2Succeeded(result, fixture, candidateOnly: false);
    }

    [Theory]
    [InlineData("missing-row", "C02_MATRIX_SCHEMA")]
    [InlineData("unknown-row", "C02_MATRIX_SCHEMA")]
    [InlineData("duplicate-row", "C02_MATRIX_SCHEMA")]
    [InlineData("changed-row", "C02_ROW_BINDING")]
    [InlineData("msix-publish", "C02_MATRIX_SCHEMA")]
    [InlineData("candidate-run", "C02_CANDIDATE_IDENTITY")]
    [InlineData("candidate-repository", "C02_CANDIDATE_IDENTITY")]
    [InlineData("candidate-commit", "C02_SOURCE_VERSION_BINDING")]
    [InlineData("candidate-workflow", "C02_CANDIDATE_SCHEMA")]
    [InlineData("p07-development", "C02_P07_SCHEMA")]
    [InlineData("p07-credential", "C02_P07_SCHEMA")]
    [InlineData("p07-source", "C02_P07_BINDING")]
    [InlineData("missing-clean-host", "C02_MISSING_FILE")]
    [InlineData("ch06-missing", "C02_CLEAN_HOST_SCHEMA")]
    [InlineData("ch06-not-run", "C02_CLEAN_HOST_SCHEMA")]
    [InlineData("ch06-na", "C02_CLEAN_HOST_SCHEMA")]
    [InlineData("ch06-fail", "C02_CLEAN_HOST_SCHEMA")]
    [InlineData("clean-host-hash", "C02_CLEAN_HOST_BINDING")]
    [InlineData("clean-host-version", "C02_SOURCE_VERSION_BINDING")]
    [InlineData("clean-host-runtime", "C02_CLEAN_HOST_BINDING")]
    [InlineData("clean-host-calendar", "C02_CLEAN_HOST_")]
    [InlineData("clean-host-future", "C02_CLEAN_HOST_TIME")]
    [InlineData("clean-host-candidate", "C02_CANDIDATE_IDENTITY")]
    [InlineData("clean-host-source", "C02_SOURCE_VERSION_BINDING")]
    [InlineData("clean-host-dependency", "C02_CLEAN_HOST_SCHEMA")]
    [InlineData("protection-actions", "C02_CLEAN_HOST_SCHEMA")]
    [InlineData("advisory-record", "C02_CLEAN_HOST_SCHEMA")]
    [InlineData("sidecar-content", "C02_SIDECAR_CONTENT")]
    [InlineData("candidate-size", "C02_DESCRIPTOR_SIZE")]
    [InlineData("candidate-hash", "C02_DESCRIPTOR_HASH")]
    [InlineData("exe-bytes", "C02_DESCRIPTOR_HASH")]
    [InlineData("os-build", "C02_OS_VERSION_BUILD")]
    [InlineData("package-host", "C02_PACKAGE_HOST_BINDING")]
    [InlineData("msix-cli", "C02_CLI_BINDING")]
    [InlineData("zip-closed-evidence", "C02_ZIP_EVIDENCE")]
    [InlineData("zip-key-shadow", "C02_ZIP_EVIDENCE")]
    [InlineData("zip-string-type", "C02_ZIP_EVIDENCE")]
    [InlineData("numeric-string", "C02_MATRIX_SCHEMA")]
    [InlineData("unknown-schema", "C02_SCHEMA_VERSION")]
    public async Task V2_rejects_broken_schema_or_semantic_closure(string mutation, string expectedCode)
    {
        using V2Fixture fixture = new();
        fixture.Mutate(mutation);
        string[] before = fixture.Snapshot();

        ProcessResult result = await fixture.ValidateAsync(TestContext.Current.CancellationToken);

        AssertV2Failed(result, expectedCode);
        Assert.Equal(before, fixture.Snapshot());
    }

    [Theory]
    [InlineData(false, "repository")]
    [InlineData(false, "run")]
    [InlineData(true, "repository")]
    [InlineData(true, "run")]
    public async Task V2_requires_explicit_repository_and_run_in_both_parameter_sets(bool candidateOnly, string omitted)
    {
        using V2Fixture fixture = new();

        ProcessResult result = await fixture.ValidateAsync(
            TestContext.Current.CancellationToken,
            candidateOnly: candidateOnly,
            expectedRepository: omitted == "repository" ? null : V2Fixture.Repository,
            expectedRunId: omitted == "run" ? null : V2Fixture.RunId);

        AssertV2Failed(result, "C02_EXPECTED_IDENTITY_REQUIRED");
    }

    [Theory]
    [InlineData("repository", "C02_CANDIDATE_IDENTITY")]
    [InlineData("run", "C02_CANDIDATE_IDENTITY")]
    [InlineData("version", "C02_SOURCE_VERSION_BINDING")]
    [InlineData("commit", "C02_SOURCE_VERSION_BINDING")]
    public async Task V2_binds_caller_expectations_instead_of_inferred_record_values(string changed, string expectedCode)
    {
        using V2Fixture fixture = new();

        ProcessResult result = await fixture.ValidateAsync(
            TestContext.Current.CancellationToken,
            expectedRepository: changed == "repository" ? "other/repository" : V2Fixture.Repository,
            expectedRunId: changed == "run" ? "999" : V2Fixture.RunId,
            expectedVersion: changed == "version" ? "0.8.1" : MatrixFixture.ProductVersion,
            expectedCommit: changed == "commit" ? new string('b', 40) : MatrixFixture.SourceCommit);

        AssertV2Failed(result, expectedCode);
    }

    [Fact]
    public async Task V2_matrix_and_candidate_parameter_sets_are_mutually_exclusive()
    {
        using V2Fixture fixture = new();

        ProcessResult result = await fixture.ValidateAsync(
            TestContext.Current.CancellationToken, conflictingParameterSets: true);

        Assert.NotEqual(0, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardOutput));
    }

    [Theory]
    [InlineData("duplicate-p07", "C02_JSON_DUPLICATE_PROPERTY")]
    [InlineData("bom-clean-host", "C02_UTF8")]
    [InlineData("invalid-utf8-clean-host", "C02_UTF8")]
    [InlineData("oversized-clean-host", "C02_FILE_SIZE_LIMIT")]
    [InlineData("oversized-candidate", "C02_FILE_SIZE_LIMIT")]
    public async Task V2_rejects_unsafe_json_before_conversion(string mutation, string expectedCode)
    {
        using V2Fixture fixture = new();
        fixture.MutateJsonBytes(mutation);

        ProcessResult result = await fixture.ValidateAsync(TestContext.Current.CancellationToken);

        AssertV2Failed(result, expectedCode);
    }

    [Theory]
    [InlineData("cli-version", "C02_ZIP_RUNTIME")]
    [InlineData("sdk-version", "C02_ZIP_RUNTIME")]
    [InlineData("cli-bytes", "C02_ZIP_CLI_HASH")]
    [InlineData("member-shadow", "C02_ZIP_RUNTIME")]
    public async Task V2_reads_the_actual_zip_cli_and_manifest_without_extracting(string mutation, string expectedCode)
    {
        using V2Fixture fixture = new();
        fixture.ReplaceZip(mutation);
        string[] before = fixture.Snapshot();

        ProcessResult result = await fixture.ValidateAsync(TestContext.Current.CancellationToken);

        AssertV2Failed(result, expectedCode);
        Assert.Equal(before, fixture.Snapshot());
    }

    private static void AssertV2Failed(ProcessResult result, string expectedCode)
    {
        Assert.NotEqual(0, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardOutput));
        Assert.Contains("C02_FAIL code=" + expectedCode, result.StandardError, StringComparison.Ordinal);
        Assert.DoesNotContain(V2Fixture.SensitiveSentinel, result.StandardError, StringComparison.Ordinal);
    }

    private static void AssertV2Succeeded(ProcessResult result, V2Fixture fixture, bool candidateOnly)
    {
        AssertProcessSucceeded(result);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError));
        using JsonDocument output = JsonDocument.Parse(result.StandardOutput);
        JsonElement value = output.RootElement;
        Assert.Equal(2, value.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(candidateOnly ? "PASS_CANDIDATE" : "PASS", value.GetProperty("status").GetString());
        Assert.Equal(3, value.GetProperty("rowCount").GetInt32());
        Assert.Equal(MatrixFixture.ProductVersion, value.GetProperty("productVersion").GetString());
        Assert.Equal(MatrixFixture.SourceCommit, value.GetProperty("sourceCommit").GetString());
        Assert.Equal(V2Fixture.Repository, value.GetProperty("candidate").GetProperty("repository").GetString());
        Assert.Equal(V2Fixture.RunId, value.GetProperty("candidate").GetProperty("runId").GetString());
        Assert.Equal(
            [V2Fixture.ExeFileName, V2Fixture.ExeSidecarFileName, MatrixFixture.ZipFileName, MatrixFixture.ZipSidecarFileName],
            value.GetProperty("publishableAssets").EnumerateArray()
                .Select(item => item.GetString() ?? throw new InvalidDataException("Public asset name is null.")).ToArray());
        Assert.Equal(
            candidateOnly ? "LOCAL_BINARY_AND_EVIDENCE" : "CANDIDATE_RECORD_ONLY",
            value.GetProperty("developmentMsixVerification").GetString());
        Assert.Equal(candidateOnly ? "NOT_RUN" : "HUMAN_RECORDED", value.GetProperty("cleanHostVerification").GetString());
        Assert.Equal("CALLER_BOUND_UPSTREAM_REQUIRED", value.GetProperty("candidateRunVerification").GetString());
        Assert.Equal("P07_RECORD_AND_PACKAGE_HASH_BOUND", value.GetProperty("exeVersionVerification").GetString());
        Assert.True(JsonNode.DeepEquals(
            fixture.Descriptor(V2Fixture.CandidateFileName), JsonNode.Parse(value.GetProperty("candidateRecord").GetRawText())));
        Assert.True(JsonNode.DeepEquals(fixture.P07["runtime"], JsonNode.Parse(value.GetProperty("runtime").GetRawText())));
        if (candidateOnly)
        {
            Assert.Equal(JsonValueKind.Null, value.GetProperty("cleanHostEvidence").ValueKind);
        }
        else
        {
            Assert.True(JsonNode.DeepEquals(
                fixture.Descriptor(V2Fixture.CleanHostFileName), JsonNode.Parse(value.GetProperty("cleanHostEvidence").GetRawText())));
        }
        Assert.Equal(4, value.GetProperty("limitations").GetArrayLength());
    }

    private static async Task AssertProcessFailedAsync(
        MatrixFixture fixture,
        string expectedMessage)
    {
        ProcessResult result = await fixture.ValidateAsync(
            TestContext.Current.CancellationToken);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(
            expectedMessage,
            result.StandardOutput + Environment.NewLine + result.StandardError,
            StringComparison.Ordinal);
    }

    private static void AssertProcessSucceeded(ProcessResult result)
    {
        Assert.True(
            result.ExitCode == 0,
            $"Matrix validator failed with exit code {result.ExitCode}.\nSTDOUT:\n{result.StandardOutput}\nSTDERR:\n{result.StandardError}");
    }

    private sealed class MatrixFixture : IDisposable
    {
        internal const string ZipFileName = "StudyReportEvaluator-win-x64.zip";
        internal const string ZipSidecarFileName = ZipFileName + ".sha256";
        internal const string ZipEvidenceFileName = "StudyReportEvaluator-win-x64.evidence.json";
        internal const string MsixFileName = "StudyReportEvaluator-win-x64.unsigned.test.msix";
        internal const string MsixSidecarFileName = MsixFileName + ".sha256";
        internal const string MsixEvidenceFileName = "StudyReportEvaluator-win-x64.unsigned.test.evidence.json";
        internal const string ProductVersion = "0.8.0";
        internal const string SourceCommit = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        internal const string EmptyUtf8Sha256 =
            "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855";
        internal static readonly UTF8Encoding Utf8NoBom = new(false, true);

        private readonly string repositoryRoot;
        private readonly string validatorPath;
        private readonly string powerShellPath;
        private readonly string matrixPath;
        private bool disposed;

        internal MatrixFixture()
        {
            repositoryRoot = FindRepositoryRoot();
            validatorPath = Path.Combine(
                repositoryRoot,
                "scripts",
                "validate-platform-release-matrix.ps1");
            powerShellPath = FindPowerShellCoreExecutable();
            DirectoryPath = Path.Combine(
                Path.GetTempPath(),
                "StudyReportEvaluator-ReleaseMatrix-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(DirectoryPath);
            matrixPath = Resolve("matrix.json");

            File.WriteAllBytes(Resolve(ZipFileName), [1, 2, 3, 4]);
            File.WriteAllBytes(Resolve(MsixFileName), [5, 6, 7, 8]);
            ZipSha256 = ComputeSha256(Resolve(ZipFileName));
            string msixSha256 = ComputeSha256(Resolve(MsixFileName));
            File.WriteAllText(
                Resolve(ZipSidecarFileName),
                $"{ZipSha256}  {ZipFileName}\n",
                Utf8NoBom);
            File.WriteAllText(
                Resolve(MsixSidecarFileName),
                $"{msixSha256}  {MsixFileName}\n",
                Utf8NoBom);

            JsonObject verification = Verification();
            JsonObject zipDescriptor = Descriptor(ZipFileName);
            JsonObject msixDescriptor = Descriptor(MsixFileName);
            ZipEvidence = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["evidenceKind"] = "windows-zip-required",
                ["status"] = "PASS_REQUIRED",
                ["sourceCommit"] = SourceCommit,
                ["sourceStatusEntryCount"] = 0,
                ["sourceStatusSha256"] = EmptyUtf8Sha256,
                ["productVersion"] = ProductVersion,
                ["host"] = verification.DeepClone(),
                ["package"] = zipDescriptor.DeepClone(),
                ["checks"] = new JsonObject
                {
                    ["sidecarVerified"] = true,
                    ["safeLayoutVerified"] = true,
                    ["bundledCliVerified"] = true,
                    ["cleanExtractVerified"] = true,
                    ["apphostLaunchVerified"] = true,
                    ["externalRuntimeAbsentVerified"] = true,
                    ["userWorkbookExcluded"] = true,
                    ["inputUnchangedVerified"] = true,
                },
            };
            WriteJson(ZipEvidenceFileName, ZipEvidence);

            JsonObject msixEvidence = new()
            {
                ["schemaVersion"] = 1,
                ["evidenceKind"] = "windows-unsigned-msix-development-mechanism",
                ["measuredAtUtc"] = DateTimeOffset.UtcNow.ToString("O"),
                ["status"] = "PASS_MECHANISM",
                ["productionStatus"] = "NOT_REQUIRED_CURRENT_SCOPE",
                ["installStatus"] = "NOT_RUN_REQUIRES_ELEVATED_DISPOSABLE_WINDOWS_11_HOST",
                ["sourceCommit"] = SourceCommit,
                ["sourceStatusEntryCount"] = 0,
                ["sourceStatusSha256"] = EmptyUtf8Sha256,
                ["host"] = new JsonObject
                {
                    ["osBuild"] = 26100,
                    ["osArchitecture"] = "X64",
                    ["processArchitecture"] = "X64",
                    ["powerShell"] = "7.6.0",
                    ["dotnetSdk"] = "10.0.400",
                },
                ["tool"] = new JsonObject
                {
                    ["packageId"] = "Microsoft.Windows.SDK.BuildTools",
                    ["packageVersion"] = "10.0.26100.4948",
                    ["packageContentHash"] = "o0T4CVaumDjPNNijKiM7p25vHKdyKqYvaVVLgQO02KTOoUDlgMYJVUQAXn1IG0G9/ZsdZ+bdgWxgQsrO/b37qw==",
                    ["makeAppxFileVersion"] = "10.0.26100.4948",
                    ["makeAppxSha256"] = new string('1', 64),
                    ["authenticodeStatus"] = "Valid",
                    ["signerSubject"] = "O=Microsoft Corporation",
                },
                ["package"] = new JsonObject
                {
                    ["fileName"] = MsixFileName,
                    ["bytes"] = msixDescriptor["bytes"]!.GetValue<long>(),
                    ["sha256"] = msixDescriptor["sha256"]!.GetValue<string>(),
                    ["entryCount"] = 1,
                    ["identityName"] = "StudyReportEvaluator.UnsignedDev",
                    ["publisher"] = "CN=StudyReportEvaluator Unsigned Development, OID.2.25.311729368913984317654407730594956997722=1",
                    ["version"] = ProductVersion + ".0",
                    ["architecture"] = "x64",
                    ["signatureEntryCount"] = 0,
                    ["blockMapHashMethod"] = "http://www.w3.org/2001/04/xmlenc#sha256",
                    ["bundledCliSha256"] = new string('2', 64),
                },
                ["syntheticAssets"] = new JsonArray(
                    SyntheticAsset("Square44x44Logo.png", 44, '3'),
                    SyntheticAsset("Square150x150Logo.png", 150, '4'),
                    SyntheticAsset("StoreLogo.png", 50, '5')),
                ["negativePolicyChecks"] = new JsonArray(
                    "INVALID_UNSIGNED_MARKER_REJECTED_WITH_OUTPUT_UNCHANGED",
                    "SIGNED_PUBLISHER_UNSIGNED_MARKER_REJECTED_WITH_OUTPUT_UNCHANGED"),
                ["limitations"] = new JsonArray(
                    "Development-only unsigned package; not for distribution.",
                    "Standard App Installer UI, non-admin setup, signature trust, and production identity are not verified.",
                    "Installation and package-installed application behavior were not run by this script."),
            };
            WriteJson(MsixEvidenceFileName, msixEvidence);

            ZipRow = new JsonObject
            {
                ["artifactKind"] = "windows-zip",
                ["platform"] = "windows",
                ["runtimeIdentifier"] = "win-x64",
                ["publish"] = true,
                ["status"] = "PASS_REQUIRED",
                ["verification"] = verification.DeepClone(),
                ["artifact"] = zipDescriptor,
                ["sidecar"] = Descriptor(ZipSidecarFileName),
                ["evidence"] = Descriptor(ZipEvidenceFileName),
            };
            JsonObject msixRow = new()
            {
                ["artifactKind"] = "windows-development-msix",
                ["platform"] = "windows",
                ["runtimeIdentifier"] = "win-x64",
                ["publish"] = false,
                ["status"] = "PASS_MECHANISM",
                ["verification"] = verification.DeepClone(),
                ["artifact"] = msixDescriptor,
                ["sidecar"] = Descriptor(MsixSidecarFileName),
                ["evidence"] = Descriptor(MsixEvidenceFileName),
            };
            Rows = new JsonArray(ZipRow, msixRow);
            Matrix = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["productVersion"] = ProductVersion,
                ["sourceCommit"] = SourceCommit,
                ["rows"] = Rows,
            };
            SaveMatrix();
        }

        internal string DirectoryPath { get; }

        internal string ZipSha256 { get; }

        internal JsonObject Matrix { get; }

        internal JsonArray Rows { get; }

        internal JsonObject ZipRow { get; }

        internal JsonObject ZipEvidence { get; }

        internal string Resolve(string fileName) => Path.Combine(DirectoryPath, fileName);

        internal JsonObject Descriptor(string fileName)
        {
            string path = Resolve(fileName);
            return new JsonObject
            {
                ["fileName"] = fileName,
                ["bytes"] = new FileInfo(path).Length,
                ["sha256"] = ComputeSha256(path),
            };
        }

        internal void WriteJson(string fileName, JsonNode value)
        {
            File.WriteAllText(
                Resolve(fileName),
                value.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
                Utf8NoBom);
        }

        internal void SaveMatrix() => WriteJson("matrix.json", Matrix);

        internal Task<ProcessResult> ValidateAsync(CancellationToken cancellationToken) =>
            RunValidatorAsync(cancellationToken,
            [
                "-MatrixPath", matrixPath,
                "-ArtifactDirectory", DirectoryPath,
                "-ExpectedProductVersion", ProductVersion,
                "-ExpectedSourceCommit", SourceCommit,
            ]);

        internal async Task<ProcessResult> RunValidatorAsync(CancellationToken cancellationToken, IEnumerable<string> arguments)
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = powerShellPath,
                WorkingDirectory = repositoryRoot,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Utf8NoBom,
                StandardErrorEncoding = Utf8NoBom,
            };
            foreach (string argument in new[]
                     {
                         "-NoLogo",
                         "-NoProfile",
                         "-NonInteractive",
                         "-CommandWithArgs",
                         // The child inherits VSTest's console code page unless set explicitly.
                         // Keep the bootstrap fixed and pass paths/values as arguments, not code.
                         "[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false, $true); " +
                         "$ErrorActionPreference = 'Stop'; $scriptPath = $args[0]; $parameters = @{}; " +
                         "for ($i = 1; $i -lt $args.Count; $i += 2) { " +
                         "$parameters.Add($args[$i].TrimStart('-'), $args[$i + 1]) }; " +
                         "& $scriptPath @parameters; if (-not $?) { exit 1 }",
                         validatorPath,
                     })
            {
                startInfo.ArgumentList.Add(argument);
            }
            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using Process process = new() { StartInfo = startInfo };
            Assert.True(process.Start());
            Task<string> output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
            Task<string> error = process.StandardError.ReadToEndAsync(CancellationToken.None);
            using CancellationTokenSource timeout =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("Release matrix validator timed out.");
            }
            finally
            {
                // Also clean up on caller cancellation, and only our own child.
                if (!process.HasExited)
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch (InvalidOperationException) when (process.HasExited)
                    {
                    }
                    using CancellationTokenSource cleanup = new(TimeSpan.FromSeconds(10));
                    await process.WaitForExitAsync(cleanup.Token);
                }
                await Task.WhenAll(output, error).WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
            }

            return new ProcessResult(
                process.ExitCode,
                await output,
                await error);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            Directory.Delete(DirectoryPath, recursive: true);
            Assert.False(Directory.Exists(DirectoryPath));
        }

        private static JsonObject Verification() => new()
        {
            ["osName"] = "Windows 11",
            ["osVersion"] = "10.0.26100.0",
            ["osBuild"] = 26100,
            ["osArchitecture"] = "X64",
            ["processArchitecture"] = "X64",
        };

        private static JsonObject SyntheticAsset(string fileName, int size, char hashDigit) => new()
        {
            ["fileName"] = fileName,
            ["width"] = size,
            ["height"] = size,
            ["sha256"] = new string(hashDigit, 64),
        };

        private static string ComputeSha256(string path)
        {
            using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Convert.ToHexString(SHA256.HashData(stream));
        }

        private static string FindPowerShellCoreExecutable()
        {
            string executableName = OperatingSystem.IsWindows() ? "pwsh.exe" : "pwsh";
            foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                         .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string candidate = Path.Combine(directory.Trim('"'), executableName);
                if (File.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }
            }

            throw new FileNotFoundException("PowerShell Core 7.4+ was not found on PATH.");
        }

        internal static string FindRepositoryRoot()
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

    private sealed class V2Fixture : IDisposable
    {
        internal const string ExeFileName = "StudyReportEvaluator-win-x64.exe";
        internal const string ExeSidecarFileName = ExeFileName + ".sha256";
        internal const string ExeEvidenceFileName = ExeFileName + ".evidence.json";
        internal const string CandidateFileName = "release-candidate-record.json";
        internal const string CleanHostFileName = "windows-singlefile-clean-host.evidence.json";
        internal const string Repository = "fixture-owner/fixture-repository";
        // Intentionally exceeds Int64: identity is a decimal STRING, never coerced.
        internal const string RunId = "98765432109876543210";
        internal const string SensitiveSentinel = "SYNTHETIC_CREDENTIAL_MUST_NOT_BE_PRINTED";
        private const string CliEntryName = "runtimes/win-x64/native/copilot.exe";
        private static readonly byte[] CliBytes = [0x43, 0x4C, 0x49, 0x31];
        private readonly MatrixFixture legacy = new();
        private readonly JsonObject msixEvidence;

        internal V2Fixture()
        {
            // Reuse the v1 evidence shape, but replace its raw ZIP bits with a
            // real tiny ZIP. Dummy EXE/MSIX/CLI bytes are never executed.
            WriteZip();
            legacy.ZipRow["artifact"] = Descriptor(MatrixFixture.ZipFileName);
            legacy.ZipEvidence["package"] = Descriptor(MatrixFixture.ZipFileName);
            WriteSidecar(MatrixFixture.ZipFileName);
            legacy.ZipRow["sidecar"] = Descriptor(MatrixFixture.ZipSidecarFileName);
            legacy.WriteJson(MatrixFixture.ZipEvidenceFileName, legacy.ZipEvidence);
            legacy.ZipRow["evidence"] = Descriptor(MatrixFixture.ZipEvidenceFileName);
            msixEvidence = JsonNode.Parse(File.ReadAllText(
                Resolve(MatrixFixture.MsixEvidenceFileName), MatrixFixture.Utf8NoBom))!.AsObject();
            msixEvidence["package"]!["bundledCliSha256"] = CliHash;
            legacy.WriteJson(MatrixFixture.MsixEvidenceFileName, msixEvidence);
            legacy.Rows[1]!["evidence"] = Descriptor(MatrixFixture.MsixEvidenceFileName);

            File.WriteAllBytes(Resolve(ExeFileName), [0x4D, 0x5A, 0x46, 0x49, 0x58, 0x54, 0x55, 0x52, 0x45]);
            WriteSidecar(ExeFileName);
            JsonObject host = legacy.ZipRow["verification"]!.DeepClone().AsObject();
            host["osName"] = "Windows"; // Actual P07 vs legacy ZIP osName.
            P07 = CreateP07(host);
            legacy.WriteJson(ExeEvidenceFileName, P07);
            ExeRow = new JsonObject
            {
                ["artifactKind"] = "windows-singlefile-exe",
                ["platform"] = "windows",
                ["runtimeIdentifier"] = "win-x64",
                ["publish"] = true,
                ["status"] = "PASS_REQUIRED",
                ["verification"] = host,
                ["artifact"] = Descriptor(ExeFileName),
                ["sidecar"] = Descriptor(ExeSidecarFileName),
                ["evidence"] = Descriptor(ExeEvidenceFileName),
            };
            Rows = new JsonArray(ExeRow, legacy.ZipRow.DeepClone(), legacy.Rows[1]!.DeepClone());
            Candidate = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["evidenceKind"] = "windows-release-candidate",
                ["productVersion"] = MatrixFixture.ProductVersion,
                ["sourceCommit"] = MatrixFixture.SourceCommit,
                ["candidate"] = Identity(),
                ["requiredTests"] = "PASS",
                ["rows"] = CandidateRows(),
            };
            legacy.WriteJson(CandidateFileName, Candidate);
            CleanHost = CreateCleanHost();
            legacy.WriteJson(CleanHostFileName, CleanHost);
            Matrix = new JsonObject
            {
                ["schemaVersion"] = 2,
                ["productVersion"] = MatrixFixture.ProductVersion,
                ["sourceCommit"] = MatrixFixture.SourceCommit,
                ["candidate"] = Identity(),
                ["candidateRecord"] = Descriptor(CandidateFileName),
                ["cleanHostEvidence"] = Descriptor(CleanHostFileName),
                ["rows"] = Rows,
            };
            SaveMatrix();
        }

        internal JsonObject Matrix { get; }
        internal JsonArray Rows { get; }
        internal JsonObject ExeRow { get; }
        internal JsonObject Candidate { get; }
        internal JsonObject P07 { get; }
        internal JsonObject CleanHost { get; }
        private static string CliHash => Convert.ToHexString(SHA256.HashData(CliBytes));

        internal string Resolve(string name) => legacy.Resolve(name);
        internal JsonObject Descriptor(string name) => legacy.Descriptor(name);
        internal void SaveMatrix() => legacy.WriteJson("matrix.json", Matrix);

        internal string[] Snapshot() => Directory.GetFileSystemEntries(legacy.DirectoryPath, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => Directory.Exists(path)
                ? "D:" + Path.GetRelativePath(legacy.DirectoryPath, path)
                : "F:" + Path.GetRelativePath(legacy.DirectoryPath, path) + ":" +
                  File.GetLastWriteTimeUtc(path).Ticks.ToString(CultureInfo.InvariantCulture) + ":" +
                  Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))))
            .ToArray();

        internal Task<ProcessResult> ValidateAsync(
            CancellationToken cancellationToken,
            bool candidateOnly = false,
            string? expectedRepository = Repository,
            string? expectedRunId = RunId,
            string expectedVersion = MatrixFixture.ProductVersion,
            string expectedCommit = MatrixFixture.SourceCommit,
            bool conflictingParameterSets = false)
        {
            List<string> arguments =
            [
                candidateOnly ? "-CandidateRecordPath" : "-MatrixPath",
                Resolve(candidateOnly ? CandidateFileName : "matrix.json"),
                "-ArtifactDirectory", legacy.DirectoryPath,
                "-ExpectedProductVersion", expectedVersion,
                "-ExpectedSourceCommit", expectedCommit,
            ];
            if (expectedRepository is not null)
            {
                arguments.AddRange(["-ExpectedRepository", expectedRepository]);
            }
            if (expectedRunId is not null)
            {
                arguments.AddRange(["-ExpectedCandidateRunId", expectedRunId]);
            }
            if (conflictingParameterSets)
            {
                arguments.AddRange(["-CandidateRecordPath", Resolve(CandidateFileName)]);
            }
            return legacy.RunValidatorAsync(cancellationToken, arguments);
        }

        internal void SaveCleanHost()
        {
            legacy.WriteJson(CleanHostFileName, CleanHost);
            Matrix["cleanHostEvidence"] = Descriptor(CleanHostFileName);
        }

        private void SaveCandidate()
        {
            legacy.WriteJson(CandidateFileName, Candidate);
            Matrix["candidateRecord"] = Descriptor(CandidateFileName);
        }

        private void RebindCandidateRows()
        {
            Candidate["rows"] = CandidateRows();
            SaveCandidate();
        }

        private JsonArray CandidateRows() => new(Rows.Select(row =>
        {
            JsonObject projected = row!.DeepClone().AsObject();
            foreach (string name in new[] { "platform", "runtimeIdentifier", "publish", "status" })
            {
                projected.Remove(name);
            }
            return (JsonNode)projected;
        }).ToArray());

        private void SaveEvidence()
        {
            legacy.WriteJson(ExeEvidenceFileName, P07);
            legacy.WriteJson(MatrixFixture.ZipEvidenceFileName, legacy.ZipEvidence);
            legacy.WriteJson(MatrixFixture.MsixEvidenceFileName, msixEvidence);
            ExeRow["evidence"] = Descriptor(ExeEvidenceFileName);
            Rows[1]!["evidence"] = Descriptor(MatrixFixture.ZipEvidenceFileName);
            Rows[2]!["evidence"] = Descriptor(MatrixFixture.MsixEvidenceFileName);
            RebindCandidateRows();
        }

        internal void Mutate(string mutation)
        {
            switch (mutation)
            {
                case "missing-row": Rows.RemoveAt(2); break;
                case "unknown-row": Rows[2]!["artifactKind"] = "other-platform"; break;
                case "duplicate-row": Rows[2] = Rows[0]!.DeepClone(); break;
                case "changed-row": Rows[2]!["artifact"]!["sha256"] = new string('0', 64); break;
                case "msix-publish": Rows[2]!["publish"] = true; break;
                case "candidate-run":
                    Candidate["candidate"]!["runId"] = "123"; SaveCandidate(); break;
                case "candidate-repository":
                    Candidate["candidate"]!["repository"] = "foreign/repository"; SaveCandidate(); break;
                case "candidate-commit":
                    Candidate["sourceCommit"] = new string('b', 40); SaveCandidate(); break;
                case "candidate-workflow":
                    Candidate["candidate"]!["workflow"] = ".github/workflows/ci.yml"; SaveCandidate(); break;
                case "p07-development": P07["status"] = "PASS_DEVELOPMENT"; SaveEvidence(); break;
                case "p07-credential": P07["runtime"]!["accessToken"] = SensitiveSentinel; SaveEvidence(); break;
                case "p07-source": P07["sourceCommit"] = new string('b', 40); SaveEvidence(); break;
                case "missing-clean-host": File.Delete(Resolve(CleanHostFileName)); break;
                case "ch06-missing": CleanHost["tests"]!.AsArray().RemoveAt(5); SaveCleanHost(); break;
                case "ch06-not-run":
                case "ch06-na":
                case "ch06-fail":
                    JsonNode required = CleanHost["tests"]![5]!;
                    required["status"] = mutation == "ch06-na" ? "N/A" : mutation == "ch06-fail" ? "FAIL" : "NOT_RUN";
                    if (mutation == "ch06-not-run")
                    {
                        required["operations"] = 0;
                        required["record"] = null;
                    }
                    SaveCleanHost();
                    break;
                case "clean-host-hash": CleanHost["package"]!["sha256"] = new string('0', 64); SaveCleanHost(); break;
                case "clean-host-version": CleanHost["productVersion"] = "0.8.1"; SaveCleanHost(); break;
                case "clean-host-runtime": CleanHost["runtime"]!["cliSha256"] = new string('0', 64); SaveCleanHost(); break;
                case "clean-host-calendar": CleanHost["measuredAtUtc"] = "2024-02-30T12:00:00Z"; SaveCleanHost(); break;
                case "clean-host-future":
                    CleanHost["measuredAtUtc"] = DateTime.UtcNow.AddHours(1).ToString("O", CultureInfo.InvariantCulture);
                    SaveCleanHost(); break;
                case "clean-host-candidate": CleanHost["candidate"]!["runId"] = "123"; SaveCleanHost(); break;
                case "clean-host-source": CleanHost["sourceCommit"] = new string('b', 40); SaveCleanHost(); break;
                case "clean-host-dependency": CleanHost["host"]!["additionalDependencies"]!["nodeNpm"] = true; SaveCleanHost(); break;
                case "protection-actions": CleanHost["protection"]!["warningActions"] = 1; SaveCleanHost(); break;
                case "advisory-record": CleanHost["tests"]![6]!["record"] = Observation("advisory.txt"); SaveCleanHost(); break;
                case "sidecar-content":
                    File.WriteAllText(Resolve(ExeSidecarFileName),
                        $"{Descriptor(ExeFileName)["sha256"]!.GetValue<string>()}  {ExeFileName}\r\n", MatrixFixture.Utf8NoBom);
                    ExeRow["sidecar"] = Descriptor(ExeSidecarFileName);
                    RebindCandidateRows(); break;
                case "candidate-size":
                    File.AppendAllText(Resolve(CandidateFileName), " ", MatrixFixture.Utf8NoBom); break;
                case "candidate-hash":
                    byte[] bytes = File.ReadAllBytes(Resolve(CandidateFileName));
                    int whitespace = Array.IndexOf(bytes, (byte)' ');
                    Assert.True(whitespace >= 0);
                    bytes[whitespace] = (byte)'\t'; // Same byte count, still JSON, different digest.
                    File.WriteAllBytes(Resolve(CandidateFileName), bytes); break;
                case "exe-bytes": File.WriteAllBytes(Resolve(ExeFileName), new byte[9]); break;
                case "os-build":
                    ExeRow["verification"]!["osBuild"] = 26200;
                    RebindCandidateRows(); break;
                case "package-host":
                    Rows[2]!["verification"]!["osBuild"] = 26200;
                    Rows[2]!["verification"]!["osVersion"] = "10.0.26200.0";
                    RebindCandidateRows(); break;
                case "msix-cli": msixEvidence["package"]!["bundledCliSha256"] = new string('0', 64); SaveEvidence(); break;
                case "zip-closed-evidence": legacy.ZipEvidence["accessToken"] = SensitiveSentinel; SaveEvidence(); break;
                case "zip-key-shadow":
                    legacy.ZipEvidence["Keys"] = new JsonArray(legacy.ZipEvidence
                        .Select(property => (JsonNode?)JsonValue.Create(property.Key)).ToArray());
                    legacy.ZipEvidence["accessToken"] = SensitiveSentinel;
                    SaveEvidence(); break;
                case "zip-string-type":
                    legacy.ZipEvidence["productVersion"] = new JsonArray(MatrixFixture.ProductVersion);
                    SaveEvidence(); break;
                case "numeric-string": ExeRow["verification"]!["osBuild"] = "26100"; break;
                case "unknown-schema": Matrix["schemaVersion"] = 3; break;
                default: throw new ArgumentOutOfRangeException(nameof(mutation));
            }
            SaveMatrix();
        }

        internal void MutateJsonBytes(string mutation)
        {
            switch (mutation)
            {
                case "duplicate-p07":
                    string json = P07.ToJsonString().Replace(
                        "\"status\":\"PASS_REQUIRED\"", "\"status\":\"PASS_REQUIRED\",\"status\":\"PASS_REQUIRED\"", StringComparison.Ordinal);
                    File.WriteAllText(Resolve(ExeEvidenceFileName), json, MatrixFixture.Utf8NoBom);
                    ExeRow["evidence"] = Descriptor(ExeEvidenceFileName);
                    RebindCandidateRows();
                    break;
                case "bom-clean-host":
                    File.WriteAllBytes(Resolve(CleanHostFileName), [0xEF, 0xBB, 0xBF, .. File.ReadAllBytes(Resolve(CleanHostFileName))]);
                    Matrix["cleanHostEvidence"] = Descriptor(CleanHostFileName);
                    break;
                case "invalid-utf8-clean-host":
                    File.WriteAllBytes(Resolve(CleanHostFileName), [0xC3, 0x28]);
                    Matrix["cleanHostEvidence"] = Descriptor(CleanHostFileName);
                    break;
                case "oversized-clean-host":
                    File.WriteAllBytes(Resolve(CleanHostFileName), new byte[(64 * 1024) + 1]);
                    Matrix["cleanHostEvidence"] = Descriptor(CleanHostFileName);
                    break;
                case "oversized-candidate":
                    File.WriteAllBytes(Resolve(CandidateFileName), new byte[(1024 * 1024) + 1]);
                    Matrix["candidateRecord"] = Descriptor(CandidateFileName);
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(mutation));
            }
            SaveMatrix();
        }

        internal void ReplaceZip(string mutation)
        {
            WriteZip(mutation);
            Rows[1]!["artifact"] = Descriptor(MatrixFixture.ZipFileName);
            legacy.ZipEvidence["package"] = Descriptor(MatrixFixture.ZipFileName);
            WriteSidecar(MatrixFixture.ZipFileName);
            Rows[1]!["sidecar"] = Descriptor(MatrixFixture.ZipSidecarFileName);
            SaveEvidence();
            SaveMatrix();
        }

        private void WriteZip(string? mutation = null)
        {
            JsonObject manifest = new()
            {
                ["schemaVersion"] = 1,
                ["runtimeIdentifier"] = "win-x64",
                ["cliVersion"] = mutation == "cli-version" ? "1.0.80" : "1.0.79",
                ["cliSha256"] = CliHash,
                ["sdkVersion"] = mutation == "sdk-version" ? "1.0.12" : "1.0.11",
                ["cliRelativePath"] = CliEntryName,
            };
            if (mutation == "member-shadow")
            {
                JsonArray keys = new(manifest.Select(property => (JsonNode?)JsonValue.Create(property.Key)).ToArray());
                manifest["Count"] = manifest.Count;
                manifest["Keys"] = keys;
                manifest["accessToken"] = SensitiveSentinel;
            }
            using FileStream file = File.Create(Resolve(MatrixFixture.ZipFileName));
            using ZipArchive archive = new(file, ZipArchiveMode.Create);
            using (Stream entry = archive.CreateEntry("StudyReportEvaluator-win-x64/copilot-runtime.json").Open())
            {
                if (mutation == "bom-manifest")
                {
                    entry.Write(new byte[] { 0xEF, 0xBB, 0xBF });
                }
                entry.Write(MatrixFixture.Utf8NoBom.GetBytes(manifest.ToJsonString()));
            }
            using (Stream entry = archive.CreateEntry("StudyReportEvaluator-win-x64/" + CliEntryName).Open())
            {
                entry.Write(mutation == "cli-bytes" ? new byte[] { 9, 8, 7, 6 } : CliBytes);
            }
        }

        private void WriteSidecar(string artifactName) => File.WriteAllText(
            Resolve(artifactName + ".sha256"),
            $"{Descriptor(artifactName)["sha256"]!.GetValue<string>()}  {artifactName}\n", MatrixFixture.Utf8NoBom);

        private static JsonObject Identity() => new()
        {
            ["repository"] = Repository,
            ["workflow"] = ".github/workflows/release.yml",
            ["runId"] = RunId,
        };

        private static JsonObject Runtime() => new()
        {
            ["dotnetSdk"] = "10.0.400",
            ["dotnetRuntime"] = "10.0.11",
            ["copilotSdk"] = "1.0.11",
            ["cliVersion"] = "1.0.79",
            ["cliSha256"] = CliHash,
        };

        internal static JsonObject Observation(string name) => new()
        {
            ["fileName"] = name,
            ["bytes"] = 42,
            ["sha256"] = new string('A', 64),
        };

        private JsonObject CreateP07(JsonObject host)
        {
            string[] methods =
            [
                "Single_file_cold_and_warm_start_validate_payload_and_show_input",
                "Single_file_missing_cached_CLI_is_recovered_by_the_standard_host",
                "Single_file_deleted_cache_is_reextracted_after_moving_the_EXE",
                "Single_file_two_instances_share_cache_and_close_independently",
                "Single_file_relative_input_and_two_prompts_are_observed_in_GUI_order",
                "Single_file_file_valued_extraction_base_fails_without_changing_data",
                "Single_file_truncated_bundle_reports_a_host_error_without_changing_data",
            ];
            return new JsonObject
            {
                ["schemaVersion"] = 1,
                ["evidenceKind"] = "windows-singlefile-required",
                ["status"] = "PASS_REQUIRED",
                ["sourceCommit"] = MatrixFixture.SourceCommit,
                ["sourceStatusEntryCount"] = 0,
                ["sourceStatusSha256"] = MatrixFixture.EmptyUtf8Sha256,
                ["sourceContentSha256"] = new string('B', 64),
                ["productVersion"] = MatrixFixture.ProductVersion,
                ["host"] = host.DeepClone(),
                ["package"] = Descriptor(ExeFileName),
                ["runtime"] = Runtime(),
                ["tests"] = new JsonObject
                {
                    ["total"] = 7, ["passed"] = 7, ["failed"] = 0, ["skipped"] = 0,
                    ["results"] = new JsonArray(methods.Select(method => (JsonNode)new JsonObject
                    {
                        ["id"] = "StudyReportEvaluator.App.Tests.Packaging.WindowsSingleFilePackageTests." + method,
                        ["status"] = "PASS",
                    }).ToArray()),
                    ["trx"] = Observation("windows-singlefile.trx"),
                },
                ["checks"] = new JsonObject
                {
                    ["singleFileVerified"] = true,
                    ["sidecarVerified"] = true,
                    ["safeLayoutVerified"] = true,
                    ["bundledCliVerified"] = true,
                    ["documentationVerified"] = true,
                    ["guiLaunchVerified"] = true,
                    ["restartAndConcurrencyVerified"] = true,
                    ["inputUnchangedVerified"] = true,
                },
                ["limitations"] = new JsonArray(
                    "Development-host observations are not clean-host evidence.",
                    "Network isolation, personal authentication and live AI were not run.",
                    "Disk-full, interrupted extraction, directory ACL faults and cross-format checkpoint resume were not run."),
            };
        }

        private JsonObject CreateCleanHost()
        {
            JsonArray tests = [];
            for (int index = 1; index <= 6; index++)
            {
                string id = "CH-" + index.ToString("D2", CultureInfo.InvariantCulture);
                tests.Add(new JsonObject
                {
                    ["id"] = id, ["status"] = "PASS", ["operations"] = 0, ["record"] = Observation(id + ".txt"),
                });
            }
            foreach (string id in new[] { "ADV-01", "ADV-02" })
            {
                tests.Add(new JsonObject { ["id"] = id, ["status"] = "NOT_RUN", ["operations"] = 0, ["record"] = null });
            }
            return new JsonObject
            {
                ["schemaVersion"] = 1,
                ["evidenceKind"] = "windows-singlefile-clean-host",
                ["measuredAtUtc"] = DateTime.UtcNow.AddMinutes(-1).ToString("O", CultureInfo.InvariantCulture),
                ["productVersion"] = MatrixFixture.ProductVersion,
                ["sourceCommit"] = MatrixFixture.SourceCommit,
                ["candidate"] = Identity(),
                ["package"] = Descriptor(ExeFileName),
                ["runtime"] = Runtime(),
                ["host"] = new JsonObject
                {
                    ["osName"] = "Windows 11",
                    ["edition"] = "Pro",
                    ["osVersion"] = "10.0.26200.0", // A different fresh host is allowed.
                    ["osBuild"] = 26200,
                    ["architecture"] = "X64",
                    ["standardUser"] = true,
                    ["fresh"] = true,
                    ["additionalDependencies"] = new JsonObject
                    {
                        ["dotnetSdk"] = false, ["dotnetRuntime"] = false, ["powerShell6Plus"] = false,
                        ["nodeNpm"] = false, ["git"] = false, ["githubCli"] = false,
                        ["copilotCli"] = false, ["office"] = false, ["ide"] = false,
                    },
                },
                ["protection"] = new JsonObject
                {
                    ["motw"] = true,
                    ["smartScreen"] = "Enabled",
                    ["smartAppControl"] = "Off",
                    ["enterprisePolicy"] = "NotConfigured",
                    ["settingsUnchanged"] = true,
                    ["launchOutcome"] = "Allowed",
                    ["launchGestures"] = 1,
                    ["warningActions"] = 0,
                    ["withinApprovedScope"] = true,
                },
                ["measurements"] = new JsonObject
                {
                    ["extractedBytes"] = 1234, ["coldStartMilliseconds"] = 20, ["warmStartMilliseconds"] = 10,
                },
                ["tests"] = tests,
            };
        }

        public void Dispose() => legacy.Dispose();
    }

    private sealed record ProcessResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}