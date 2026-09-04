using System.Diagnostics;
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

        internal async Task<ProcessResult> ValidateAsync(CancellationToken cancellationToken)
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = powerShellPath,
                WorkingDirectory = repositoryRoot,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (string argument in new[]
                     {
                         "-NoLogo",
                         "-NoProfile",
                         "-NonInteractive",
                         "-File",
                         validatorPath,
                         "-MatrixPath",
                         matrixPath,
                         "-ArtifactDirectory",
                         DirectoryPath,
                         "-ExpectedProductVersion",
                         ProductVersion,
                         "-ExpectedSourceCommit",
                         SourceCommit,
                     })
            {
                startInfo.ArgumentList.Add(argument);
            }

            using Process process = new() { StartInfo = startInfo };
            Assert.True(process.Start());
            Task<string> output = process.StandardOutput.ReadToEndAsync(cancellationToken);
            Task<string> error = process.StandardError.ReadToEndAsync(cancellationToken);
            using CancellationTokenSource timeout =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
                throw new TimeoutException("Release matrix validator timed out.");
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

    private sealed record ProcessResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}