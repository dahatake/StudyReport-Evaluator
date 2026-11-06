using Xunit;

namespace StudyReportEvaluator.App.Tests.Packaging;

public sealed class ReleaseWorkflowContractTests
{
    [Fact]
    public void Candidate_workflow_is_manual_stable_tag_only_and_never_publishes()
    {
        string workflow = ReadWorkflow("release.yml");
        string normalized = NormalizeNewlines(workflow);

        Assert.Contains("workflow_dispatch:", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("on:\n  push:", normalized, StringComparison.Ordinal);
        Assert.Contains("'^v(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)$'", workflow, StringComparison.Ordinal);
        Assert.Contains("The release tag must be annotated.", workflow, StringComparison.Ordinal);
        Assert.Contains("The release checkout must be clean.", workflow, StringComparison.Ordinal);
        Assert.Contains("PowerShell Core 7.5 or later is required.", workflow, StringComparison.Ordinal);
        Assert.Contains("--draft", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("--draft=false", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("gh release edit", workflow, StringComparison.Ordinal);
        Assert.Contains("this workflow never replaces a release.", workflow, StringComparison.Ordinal);
        Assert.Contains("PASS_CANDIDATE is non-publishable", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void Candidate_workflow_builds_all_three_candidates_and_invokes_c03_candidate_with_identity_binding()
    {
        string workflow = ReadWorkflow("release.yml");

        Assert.Contains("dotnet --list-runtimes", workflow, StringComparison.Ordinal);
        Assert.Contains("WindowsDesktop runtime 10.0.x is required for single-file UIA observer validation.", workflow, StringComparison.Ordinal);
        Assert.Contains(".\\scripts\\publish-windows.ps1 -SingleFile", workflow, StringComparison.Ordinal);
        Assert.Contains(".\\scripts\\package-windows-singlefile.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains(".\\scripts\\test-windows-singlefile.ps1 -ResultsDirectory .\\TestResults\\release\\windows-singlefile", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("test-windows-singlefile.ps1 -ResultsDirectory .\\TestResults\\release\\windows-singlefile -DevelopmentOnly", workflow, StringComparison.Ordinal);
        Assert.Contains(".\\scripts\\test-windows-zip.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains(".\\scripts\\test-windows-msix-unsigned.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains(
            "FullyQualifiedName!~StudyReportEvaluator.App.Tests.Packaging.WindowsSingleFilePackageTests",
            workflow,
            StringComparison.Ordinal);
        Assert.Contains("-Mode Candidate", workflow, StringComparison.Ordinal);
        Assert.Contains("-ExpectedRepository $env:GITHUB_REPOSITORY", workflow, StringComparison.Ordinal);
        Assert.Contains("-CandidateRunId $env:GITHUB_RUN_ID", workflow, StringComparison.Ordinal);
        Assert.Contains("-SingleFileDirectory .\\artifacts\\package", workflow, StringComparison.Ordinal);
        Assert.Contains("-ZipDirectory .\\artifacts\\package", workflow, StringComparison.Ordinal);
        Assert.Contains("-MsixDirectory .\\artifacts\\package\\mechanism", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void Candidate_workflow_uses_exact_control_and_public_artifact_sets_with_no_msix_binary()
    {
        string workflow = ReadWorkflow("release.yml");
        string normalized = NormalizeNewlines(workflow);

        string controlUpload = SliceSection(normalized, "Upload release control evidence");
        Assert.Equal(
            new[]
            {
                "artifacts/package/matrix/StudyReportEvaluator-win-x64.evidence.json",
                "artifacts/package/matrix/StudyReportEvaluator-win-x64.exe.evidence.json",
                "artifacts/package/matrix/StudyReportEvaluator-win-x64.unsigned.test.evidence.json",
                "artifacts/package/matrix/StudyReportEvaluator-win-x64.unsigned.test.msix.sha256",
                "artifacts/package/matrix/release-candidate-record.json",
            },
            ExtractUploadPaths(controlUpload));

        string publicUpload = SliceSection(normalized, "Upload public candidate assets");
        Assert.Equal(
            new[]
            {
                "artifacts/package/matrix/StudyReportEvaluator-win-x64.exe",
                "artifacts/package/matrix/StudyReportEvaluator-win-x64.exe.sha256",
                "artifacts/package/matrix/StudyReportEvaluator-win-x64.zip",
                "artifacts/package/matrix/StudyReportEvaluator-win-x64.zip.sha256",
            },
            ExtractUploadPaths(publicUpload));
        Assert.Contains("compression-level: 0", publicUpload, StringComparison.Ordinal);

        string releaseCreate = SliceSection(normalized, "Create the draft GitHub Release");
        // FR-121 AC-028/AC-102: mutations now use the same byte-verifying driver as
        // the independent HTTP contracts, rather than a second gh upload path.
        Assert.Contains("invoke-release-automation.ps1", releaseCreate, StringComparison.Ordinal);
        string driver = ReadScript("release_automation.py");
        Assert.Contains("PUBLIC = (EXE, EXE + \".sha256\", ZIP, ZIP + \".sha256\")", driver, StringComparison.Ordinal);
        Assert.Contains("for name in PUBLIC:", driver, StringComparison.Ordinal);
        Assert.Contains("api.call(\"POST\", upload", driver, StringComparison.Ordinal);
        Assert.DoesNotContain("gh release", driver, StringComparison.Ordinal);
    }

    [Fact]
    public void Publish_workflow_requires_protected_environment_permissions_and_clean_host_json_via_env_only()
    {
        string workflow = ReadWorkflow("publish-release.yml");
        string normalized = NormalizeNewlines(workflow);

        Assert.Contains("environment: publish", workflow, StringComparison.Ordinal);
        Assert.Contains("actions: read", workflow, StringComparison.Ordinal);
        Assert.Contains("contents: write", workflow, StringComparison.Ordinal);
        Assert.Contains("clean_host_evidence_json:", workflow, StringComparison.Ordinal);
        Assert.Contains("CLEAN_HOST_EVIDENCE_JSON: ${{ inputs.clean_host_evidence_json }}", workflow, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(workflow, "${{ inputs.clean_host_evidence_json }}"));
        Assert.Contains("windows-singlefile-clean-host.evidence.json", workflow, StringComparison.Ordinal);
        Assert.Contains("[System.Text.UTF8Encoding]::new($false, $true)", workflow, StringComparison.Ordinal);
        Assert.Contains("65536", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("Write-Output $env:CLEAN_HOST_EVIDENCE_JSON", normalized, StringComparison.Ordinal);
    }

    [Fact]
    public void Publish_workflow_binds_candidate_run_to_repository_event_workflow_path_conclusion_and_sha()
    {
        string workflow = ReadWorkflow("publish-release.yml");

        Assert.Contains("actions/runs/$env:CANDIDATE_RUN_ID", workflow, StringComparison.Ordinal);
        Assert.Contains("$workflowPath -csplit '@', 2", workflow, StringComparison.Ordinal);
        Assert.Contains(".github/workflows/release.yml", workflow, StringComparison.Ordinal);
        Assert.Contains("\\Arefs/(?:heads|tags)/", workflow, StringComparison.Ordinal);
        Assert.Contains("[string]$run.repository.full_name -cne $env:GITHUB_REPOSITORY", workflow, StringComparison.Ordinal);
        Assert.Contains("[string]$run.event -cne 'workflow_dispatch'", workflow, StringComparison.Ordinal);
        Assert.Contains("[string]$run.conclusion -cne 'success'", workflow, StringComparison.Ordinal);
        Assert.Contains("[string]$run.head_sha -cne $tagCommit", workflow, StringComparison.Ordinal);
        Assert.Contains("if ($LASTEXITCODE -ne 0 -or $runJson.Count -eq 0)", workflow, StringComparison.Ordinal);
        // FR-121 AC-100: the typed Actions download now binds the original run.
        Assert.Contains("run-id: ${{ inputs.candidate_run_id }}", workflow, StringComparison.Ordinal);
        Assert.Contains("name: release-candidate-${{ inputs.tag }}-${{ inputs.candidate_run_id }}", workflow, StringComparison.Ordinal);
        Assert.Contains("[string]$run.status -cne 'completed'", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void Publish_workflow_enforces_exact_control_and_draft_asset_sets_invokes_c03_final_and_uploads_before_publish()
    {
        string workflow = ReadWorkflow("publish-release.yml");
        string normalized = NormalizeNewlines(workflow);

        Assert.Contains("release-candidate-record.json", workflow, StringComparison.Ordinal);
        Assert.Contains("StudyReportEvaluator-win-x64.exe.evidence.json", workflow, StringComparison.Ordinal);
        Assert.Contains("StudyReportEvaluator-win-x64.evidence.json", workflow, StringComparison.Ordinal);
        Assert.Contains("StudyReportEvaluator-win-x64.unsigned.test.msix.sha256", workflow, StringComparison.Ordinal);
        Assert.Contains("StudyReportEvaluator-win-x64.unsigned.test.evidence.json", workflow, StringComparison.Ordinal);
        // FR-121 AC-028: C03 retains the exact private sets; the shared driver
        // independently rejects missing/extra public names and verifies downloads.
        string driver = ReadScript("release_automation.py");
        string builder = ReadScript("build-platform-release-matrix.ps1");
        Assert.Contains("Assert-C03ExactFileSet", builder, StringComparison.Ordinal);
        Assert.Contains("-WithoutArtifact", builder, StringComparison.Ordinal);
        Assert.Contains("len(listing) == 4", driver, StringComparison.Ordinal);
        Assert.Contains("{v[\"name\"] for v in listing} == set(PUBLIC)", driver, StringComparison.Ordinal);
        Assert.Contains("downloaded == assets[value[\"name\"]]", driver, StringComparison.Ordinal);
        Assert.Contains("-Mode Final", workflow, StringComparison.Ordinal);
        Assert.Contains("-ExpectedRepository $env:GITHUB_REPOSITORY", workflow, StringComparison.Ordinal);
        Assert.Contains("-CandidateRunId $env:CANDIDATE_RUN_ID", workflow, StringComparison.Ordinal);
        Assert.Contains("-CandidateRecordPath (Join-Path $candidateRoot 'release-candidate-record.json')", workflow, StringComparison.Ordinal);
        Assert.Contains("-CleanHostEvidencePath (Join-Path $env:RUNNER_TEMP 'windows-singlefile-clean-host.evidence.json')", workflow, StringComparison.Ordinal);

        int uploadIndex = normalized.IndexOf("- name: Upload final publish control evidence", StringComparison.Ordinal);
        int publishIndex = normalized.IndexOf("- name: Revalidate exact draft bytes and publish", StringComparison.Ordinal);
        Assert.True(uploadIndex > 0 && publishIndex > uploadIndex);
        Assert.Contains("invoke-release-automation.ps1", normalized[publishIndex..], StringComparison.Ordinal);
        Assert.Contains("needs: validate", workflow, StringComparison.Ordinal);
        Assert.Contains("existing release bytes differ", driver, StringComparison.Ordinal);

        string finalControlUpload = SliceSection(normalized, "Upload final publish control evidence");
        Assert.Equal(
            new[]
            {
                "${{ runner.temp }}/final-control/StudyReportEvaluator-win-x64.evidence.json",
                "${{ runner.temp }}/final-control/StudyReportEvaluator-win-x64.exe.evidence.json",
                "${{ runner.temp }}/final-control/StudyReportEvaluator-win-x64.unsigned.test.evidence.json",
                "${{ runner.temp }}/final-control/StudyReportEvaluator-win-x64.unsigned.test.msix.sha256",
                "${{ runner.temp }}/final-control/platform-release-matrix.json",
                "${{ runner.temp }}/final-control/release-candidate-record.json",
                "${{ runner.temp }}/final-control/windows-singlefile-clean-host.evidence.json",
            },
            ExtractUploadPaths(finalControlUpload));
    }

    [Fact]
    public void Publish_workflow_revalidates_current_bytes_without_resetting_public_release_to_draft()
    {
        string workflow = NormalizeNewlines(ReadWorkflow("publish-release.yml"));
        string publishStep = SliceSection(workflow, "Revalidate exact draft bytes and publish");

        // FR-121 AC-102 explicitly forbids the former rollback-to-draft policy.
        // Byte validation and post-transition confirmation remain mandatory,
        // now in the production driver exercised by the independent HTTP tests.
        Assert.Contains("invoke-release-automation.ps1", publishStep, StringComparison.Ordinal);
        string driver = ReadScript("release_automation.py");
        string adapter = ReadScript("prepare_release_automation.py");
        Assert.Contains("scripts/validate-platform-release-matrix.ps1", adapter, StringComparison.Ordinal);
        Assert.Contains("validate_controls(root, identity, final=True)", adapter, StringComparison.Ordinal);
        Assert.Contains("binary=True", driver, StringComparison.Ordinal);
        Assert.Contains("downloaded == assets[value[\"name\"]]", driver, StringComparison.Ordinal);
        int before = driver.IndexOf("result[\"stage\"] = \"pre-publication-identity\"", StringComparison.Ordinal);
        int publish = driver.IndexOf("api.call(\"PATCH\"", StringComparison.Ordinal);
        int after = driver.IndexOf("result[\"stage\"] = \"confirm-public\"", StringComparison.Ordinal);
        Assert.True(before >= 0 && publish > before && after > publish);
        Assert.Contains("verify_release(api, request, assets, release)", driver[after..], StringComparison.Ordinal);
        Assert.Contains("require(release[\"draft\"] is False", driver[after..], StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(driver, "api.call(\"PATCH\""));
        Assert.DoesNotContain("--draft=true", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("{\"draft\": True}", driver, StringComparison.Ordinal);
        Assert.DoesNotContain("api.call(\"DELETE\"", driver, StringComparison.Ordinal);
        Assert.Contains("etag=etag", driver, StringComparison.Ordinal);
    }

    [Fact]
    public void Publish_workflow_never_builds_repackages_reuploads_or_tags()
    {
        string workflow = ReadWorkflow("publish-release.yml");

        Assert.DoesNotContain("gh release create", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("gh release upload", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("gh release delete", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("dotnet build", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("publish-windows.ps1", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("package-windows", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("test-windows", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("git push", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("git tag", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void C03_builder_contract_requires_powershell_7_5_and_candidate_non_publishable_status()
    {
        string repositoryRoot = FindRepositoryRoot();
        string script = File.ReadAllText(Path.Combine(repositoryRoot, "scripts", "build-platform-release-matrix.ps1"));

        Assert.Contains("#Requires -Version 7.5", script, StringComparison.Ordinal);
        Assert.Contains("#Requires -PSEdition Core", script, StringComparison.Ordinal);
        Assert.Contains("C03_PASS status=PASS_CANDIDATE publicationEligible=false", script, StringComparison.Ordinal);
        Assert.Contains("PASS_CANDIDATE is not publication eligible", script, StringComparison.Ordinal);
    }

    private static string SliceSection(string yaml, string stepName)
    {
        string marker = $"- name: {stepName}";
        int start = yaml.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Step not found: {stepName}");

        int next = yaml.IndexOf("\n      - name: ", start + marker.Length, StringComparison.Ordinal);
        return next < 0 ? yaml[start..] : yaml[start..next];
    }

    private static string ReadScript(string name) =>
        File.ReadAllText(Path.Combine(FindRepositoryRoot(), "scripts", name));

    private static string[] ExtractUploadPaths(string step)
    {
        string normalized = NormalizeNewlines(step);
        const string marker = "          path: |\n";
        int start = normalized.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, "Upload path block was not found.");

        return normalized[(start + marker.Length)..]
            .Split('\n')
            .TakeWhile(line => line.StartsWith("            ", StringComparison.Ordinal))
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] ExtractReleaseCreatePaths(string step) =>
        NormalizeNewlines(step)
            .Split('\n')
            .Select(line => line.Trim().TrimEnd(',').Trim('\''))
            .Where(line => line.StartsWith(
                ".\\artifacts\\package\\matrix\\StudyReportEvaluator-win-x64.",
                StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static int CountOccurrences(string text, string needle)
    {
        if (needle.Length == 0)
        {
            return 0;
        }

        int count = 0;
        int start = 0;
        while (true)
        {
            int index = text.IndexOf(needle, start, StringComparison.Ordinal);
            if (index < 0)
            {
                return count;
            }

            count++;
            start = index + needle.Length;
        }
    }

    private static string NormalizeNewlines(string value) => value.Replace("\r\n", "\n");

    private static string ReadWorkflow(string fileName) =>
        File.ReadAllText(Path.Combine(FindRepositoryRoot(), ".github", "workflows", fileName));

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

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
