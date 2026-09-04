using Xunit;

namespace StudyReportEvaluator.App.Tests.Packaging;

public sealed class ReleaseWorkflowContractTests
{
    [Fact]
    public void Candidate_workflow_only_ever_creates_a_draft_for_a_stable_annotated_tag()
    {
        string workflow = ReadWorkflow("release.yml");

        Assert.Contains("workflow_dispatch:", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("on:\n  push:", workflow.Replace("\r\n", "\n"), StringComparison.Ordinal);
        Assert.Contains("actions/setup-dotnet@v6", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("actions/setup-dotnet@v4", workflow, StringComparison.Ordinal);

        Assert.Contains("'^v(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)$'", workflow, StringComparison.Ordinal);
        Assert.Contains("The release tag must be annotated.", workflow, StringComparison.Ordinal);
        Assert.Contains("The release checkout must be clean.", workflow, StringComparison.Ordinal);
        Assert.Contains("CHANGELOG.md does not contain a dated $version section.", workflow, StringComparison.Ordinal);

        Assert.Contains("--draft", workflow, StringComparison.Ordinal);
        Assert.Contains("--verify-tag", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("--draft=false", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("--clobber", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("--prerelease", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("inputs.draft", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("gh release edit", workflow, StringComparison.Ordinal);
        Assert.Contains("this workflow never replaces a release.", workflow, StringComparison.Ordinal);
        Assert.Contains(
            "The candidate release must remain a draft that carries exactly the Windows ZIP and its sidecar.",
            workflow,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Candidate_workflow_validates_every_required_artifact_before_creating_the_draft()
    {
        string workflow = ReadWorkflow("release.yml");

        Assert.Contains(".\\scripts\\test-windows-zip.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains(".\\scripts\\test-windows-msix-unsigned.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains(".\\scripts\\build-platform-release-matrix.ps1", workflow, StringComparison.Ordinal);

        int matrixIndex = workflow.IndexOf(
            ".\\scripts\\build-platform-release-matrix.ps1",
            StringComparison.Ordinal);
        int draftIndex = workflow.IndexOf("Create the draft GitHub Release", StringComparison.Ordinal);
        Assert.True(matrixIndex > 0 && draftIndex > matrixIndex);
    }

    [Fact]
    public void Candidate_workflow_publishes_only_the_ZIP_and_keeps_control_evidence_off_the_release()
    {
        string workflow = ReadWorkflow("release.yml");

        Assert.Contains("artifacts/package/matrix/platform-release-matrix.json", workflow, StringComparison.Ordinal);
        Assert.Contains("artifacts/package/StudyReportEvaluator-win-x64.evidence.json", workflow, StringComparison.Ordinal);
        Assert.Contains(
            "artifacts/package/mechanism/StudyReportEvaluator-win-x64.unsigned.test.evidence.json",
            workflow,
            StringComparison.Ordinal);

        string releaseCreateSection = workflow[workflow.IndexOf(
            "Create the draft GitHub Release",
            StringComparison.Ordinal)..];
        Assert.Contains("StudyReportEvaluator-win-x64.zip", releaseCreateSection, StringComparison.Ordinal);
        Assert.Contains("StudyReportEvaluator-win-x64.zip.sha256", releaseCreateSection, StringComparison.Ordinal);
        Assert.DoesNotContain(".unsigned.test.msix", releaseCreateSection, StringComparison.Ordinal);
        Assert.DoesNotContain("platform-release-matrix.json", releaseCreateSection, StringComparison.Ordinal);
    }

    [Fact]
    public void Publish_workflow_requires_a_protected_environment_and_an_existing_draft()
    {
        string workflow = ReadWorkflow("publish-release.yml");

        Assert.Contains("environment: publish", workflow, StringComparison.Ordinal);
        Assert.Contains("workflow_dispatch:", workflow, StringComparison.Ordinal);
        Assert.Contains("candidate_run_id:", workflow, StringComparison.Ordinal);
        Assert.Contains(
            "Only an existing draft release can be published by this workflow.",
            workflow,
            StringComparison.Ordinal);
        Assert.Contains(
            "The candidate run must have succeeded for the exact tagged commit.",
            workflow,
            StringComparison.Ordinal);
        Assert.Contains("The publish checkout must be clean.", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void Publish_workflow_never_builds_replaces_or_tags_release_artifacts()
    {
        string workflow = ReadWorkflow("publish-release.yml");

        Assert.DoesNotContain("--clobber", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("gh release create", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("gh release upload", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("gh release delete", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("git tag", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("git push", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("package-windows.ps1", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("test-windows-zip.ps1", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("dotnet build", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void Publish_workflow_verifies_matrix_bound_assets_before_the_final_publish_step()
    {
        string workflow = ReadWorkflow("publish-release.yml");

        Assert.Contains(
            "Exactly one publishable Windows ZIP row must be PASS_REQUIRED.",
            workflow,
            StringComparison.Ordinal);
        Assert.Contains(
            "The development MSIX row must remain a non-published mechanism row.",
            workflow,
            StringComparison.Ordinal);
        Assert.Contains(
            "The draft must carry exactly the Windows ZIP and its SHA-256 sidecar.",
            workflow,
            StringComparison.Ordinal);
        Assert.Contains(
            "The draft assets do not match the validated release matrix.",
            workflow,
            StringComparison.Ordinal);
        Assert.Contains(
            "The published sidecar bytes must exactly describe the published package.",
            workflow,
            StringComparison.Ordinal);

        int verificationIndex = workflow.IndexOf(
            "The draft assets do not match the validated release matrix.",
            StringComparison.Ordinal);
        int publishIndex = workflow.IndexOf("--draft=false", StringComparison.Ordinal);
        Assert.True(verificationIndex > 0 && publishIndex > verificationIndex);
        Assert.Equal(publishIndex, workflow.LastIndexOf("--draft=false", StringComparison.Ordinal));
    }

    [Fact]
    public void Release_matrix_writer_fails_closed_and_only_marks_the_ZIP_publishable()
    {
        string repositoryRoot = FindRepositoryRoot();
        string script = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "scripts",
            "build-platform-release-matrix.ps1"));

        Assert.Contains("#Requires -Version 7.4", script, StringComparison.Ordinal);
        Assert.Contains("#Requires -PSEdition Core", script, StringComparison.Ordinal);
        Assert.Contains(
            "The release matrix must be generated from a clean source checkout.",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "Release matrix evidence was not produced from the checked-out source commit.",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "Release matrix evidence was measured on different Windows hosts.",
            script,
            StringComparison.Ordinal);
        Assert.Contains("publish = $true", script, StringComparison.Ordinal);
        Assert.Contains("publish = $false", script, StringComparison.Ordinal);
        Assert.Contains("status = 'PASS_REQUIRED'", script, StringComparison.Ordinal);
        Assert.Contains("status = 'PASS_MECHANISM'", script, StringComparison.Ordinal);
        Assert.Contains("validate-platform-release-matrix.ps1", script, StringComparison.Ordinal);
        Assert.DoesNotContain("PASS_PRODUCTION", script, StringComparison.Ordinal);

        int validatorIndex = script.IndexOf("& $validator", StringComparison.Ordinal);
        int successMessageIndex = script.IndexOf("Publishable assets:", StringComparison.Ordinal);
        Assert.True(validatorIndex > 0 && successMessageIndex > validatorIndex);
    }

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
