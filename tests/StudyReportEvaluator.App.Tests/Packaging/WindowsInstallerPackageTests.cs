using System.Xml.Linq;
using Xunit;

namespace StudyReportEvaluator.App.Tests.Packaging;

public sealed class WindowsInstallerPackageTests
{
    [Fact]
    public void Msix_manifest_template_declares_the_x64_packaged_classic_full_trust_contract()
    {
        string repositoryRoot = FindRepositoryRoot();
        string path = Path.Combine(repositoryRoot, "eng", "packaging", "windows", "AppxManifest.xml");
        XDocument document = XDocument.Load(path, LoadOptions.PreserveWhitespace);
        XNamespace foundation = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
        XNamespace uap = "http://schemas.microsoft.com/appx/manifest/uap/windows10";
        XNamespace uap10 = "http://schemas.microsoft.com/appx/manifest/uap/windows10/10";
        XNamespace rescap = "http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities";

        XElement package = Assert.IsType<XElement>(document.Root);
        Assert.Equal(foundation + "Package", package.Name);
        XElement identity = Assert.Single(package.Elements(foundation + "Identity"));
        Assert.Equal("@IDENTITY_NAME@", identity.Attribute("Name")?.Value);
        Assert.Equal("@PUBLISHER@", identity.Attribute("Publisher")?.Value);
        Assert.Equal("@PACKAGE_VERSION@", identity.Attribute("Version")?.Value);
        Assert.Equal("x64", identity.Attribute("ProcessorArchitecture")?.Value);

        XElement targetFamily = Assert.Single(
            package.Descendants(foundation + "TargetDeviceFamily"));
        Assert.Equal("Windows.Desktop", targetFamily.Attribute("Name")?.Value);
        Assert.Equal("10.0.22000.0", targetFamily.Attribute("MinVersion")?.Value);

        XElement capability = Assert.Single(package.Descendants(rescap + "Capability"));
        Assert.Equal("runFullTrust", capability.Attribute("Name")?.Value);

        XElement application = Assert.Single(package.Descendants(foundation + "Application"));
        Assert.Equal("StudyReportEvaluator.App.exe", application.Attribute("Executable")?.Value);
        Assert.Equal("packagedClassicApp", application.Attribute(uap10 + "RuntimeBehavior")?.Value);
        Assert.Equal("mediumIL", application.Attribute(uap10 + "TrustLevel")?.Value);

        XElement visualElements = Assert.Single(application.Elements(uap + "VisualElements"));
        Assert.Equal("Assets\\Square44x44Logo.png", visualElements.Attribute("Square44x44Logo")?.Value);
        Assert.Equal("Assets\\Square150x150Logo.png", visualElements.Attribute("Square150x150Logo")?.Value);
    }

    [Fact]
    public void Msix_mechanism_script_is_test_only_and_fails_closed_on_identity_assets_and_signature()
    {
        string repositoryRoot = FindRepositoryRoot();
        string path = Path.Combine(repositoryRoot, "scripts", "package-windows-msix.ps1");
        string source = File.ReadAllText(path);

        Assert.Contains("#Requires -Version 7.0", source, StringComparison.Ordinal);
        Assert.Contains("#Requires -PSEdition Core", source, StringComparison.Ordinal);
        Assert.Contains("StudyReportEvaluator-win-x64.test.msix", source, StringComparison.Ordinal);
        Assert.DoesNotContain("StudyReportEvaluator-win-x64.msix'", source, StringComparison.Ordinal);
        foreach (string requiredParameter in new[]
        {
            "$IdentityName",
            "$Publisher",
            "$PublisherDisplayName",
            "$Square44x44LogoPath",
            "$Square150x150LogoPath",
            "$StoreLogoPath",
            "$TestCertificateThumbprint",
        })
        {
            Assert.Contains(requiredParameter, source, StringComparison.Ordinal);
        }

        Assert.Contains("Windows SDK x64 MakeAppx.exe and SignTool.exe are required", source, StringComparison.Ordinal);
        Assert.Contains("$certificate.Subject, $Publisher", source, StringComparison.Ordinal);
        Assert.Contains("1.3.6.1.5.5.7.3.3", source, StringComparison.Ordinal);
        Assert.Contains("'sign', '/fd', 'SHA256', '/sha1'", source, StringComparison.Ordinal);
        Assert.Contains("'verify', '/pa', '/v'", source, StringComparison.Ordinal);
        Assert.Contains("AppxBlockMap.xml", source, StringComparison.Ordinal);
        Assert.Contains("http://www.w3.org/2001/04/xmlenc#sha256", source, StringComparison.Ordinal);
        Assert.Contains("PASS_MECHANISM only", source, StringComparison.Ordinal);
        Assert.Contains("Assert-PathWithinRoot -Root $repositoryRoot -Path $PublishedDirectory", source, StringComparison.Ordinal);
        Assert.Contains("Assert-NotReparsePoint", source, StringComparison.Ordinal);
        Assert.Contains("Assert-SafePublishPayload", source, StringComparison.Ordinal);
        Assert.Contains("$PublicPayloadRelativePaths", source, StringComparison.Ordinal);
        Assert.Contains("'docs\\getting-started.md'", source, StringComparison.Ordinal);
        Assert.Contains("'images\\07-output-export.png'", source, StringComparison.Ordinal);
        Assert.Contains("$hasCodeSigningEku", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Import-Certificate", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TrustedPeople", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Windows_SDK_build_tools_are_exactly_locked_for_reproducible_MSIX_packaging()
    {
        string repositoryRoot = FindRepositoryRoot();
        string toolRoot = Path.Combine(repositoryRoot, "eng", "packaging", "windows", "tools");
        XDocument project = XDocument.Load(Path.Combine(toolRoot, "WindowsSdkBuildTools.csproj"));
        XElement package = Assert.Single(project.Descendants("PackageReference"));
        Assert.Equal("Microsoft.Windows.SDK.BuildTools", package.Attribute("Include")?.Value);
        Assert.Equal("[10.0.26100.4948]", package.Attribute("Version")?.Value);
        Assert.Equal("all", package.Attribute("PrivateAssets")?.Value);

        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(
            File.ReadAllText(Path.Combine(toolRoot, "packages.lock.json")));
        System.Text.Json.JsonElement dependency = document.RootElement
            .GetProperty("dependencies")
            .GetProperty("net10.0")
            .GetProperty("Microsoft.Windows.SDK.BuildTools");
        Assert.Equal(
            "[10.0.26100.4948, 10.0.26100.4948]",
            dependency.GetProperty("requested").GetString());
        Assert.Equal("10.0.26100.4948", dependency.GetProperty("resolved").GetString());
        Assert.Equal(
            "o0T4CVaumDjPNNijKiM7p25vHKdyKqYvaVVLgQO02KTOoUDlgMYJVUQAXn1IG0G9/ZsdZ+bdgWxgQsrO/b37qw==",
            dependency.GetProperty("contentHash").GetString());
    }

    [Fact]
    public void Unsigned_MSIX_driver_uses_locked_Microsoft_tools_and_emits_mechanism_only_evidence()
    {
        string repositoryRoot = FindRepositoryRoot();
        string path = Path.Combine(repositoryRoot, "scripts", "test-windows-msix-unsigned.ps1");
        string source = File.ReadAllText(path);

        Assert.Contains("#Requires -Version 7.0", source, StringComparison.Ordinal);
        Assert.Contains("#Requires -PSEdition Core", source, StringComparison.Ordinal);
        Assert.Contains("Microsoft.Windows.SDK.BuildTools", source, StringComparison.Ordinal);
        Assert.Contains("10.0.26100.4948", source, StringComparison.Ordinal);
        Assert.Contains("--locked-mode", source, StringComparison.Ordinal);
        Assert.Contains("Get-AuthenticodeSignature", source, StringComparison.Ordinal);
        Assert.Contains("O=Microsoft Corporation", source, StringComparison.Ordinal);
        Assert.Contains("System.Drawing.Common", source, StringComparison.Ordinal);
        Assert.Contains("[AllowEmptyString()][string] $Value", source, StringComparison.Ordinal);
        Assert.Contains("$versionResult = & $versionTool show -Json", source, StringComparison.Ordinal);
        Assert.Contains("$packageVersion = \"$productVersion.0\"", source, StringComparison.Ordinal);
        Assert.Contains("Test-OwnedOutputSetValid", source, StringComparison.Ordinal);
        Assert.Contains("Assert-ExpectedPolicyFailure", source, StringComparison.Ordinal);
        Assert.Contains("INVALID_UNSIGNED_MARKER_REJECTED_WITH_OUTPUT_UNCHANGED", source, StringComparison.Ordinal);
        Assert.Contains("SIGNED_PUBLISHER_UNSIGNED_MARKER_REJECTED_WITH_OUTPUT_UNCHANGED", source, StringComparison.Ordinal);
        Assert.Contains("-UnsignedDevelopment", source, StringComparison.Ordinal);
        Assert.Contains("AppxSignature.p7x", source, StringComparison.Ordinal);
        Assert.Contains("docs/getting-started.md", source, StringComparison.Ordinal);
        Assert.Contains("images/07-output-export.png", source, StringComparison.Ordinal);
        Assert.Contains("PASS_MECHANISM", source, StringComparison.Ordinal);
        Assert.Contains("BLOCKED_EXTERNAL", source, StringComparison.Ordinal);
        Assert.Contains("NOT_RUN_REQUIRES_ELEVATED_DISPOSABLE_WINDOWS_11_HOST", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProductVersion = '0.8.0'", source, StringComparison.Ordinal);
        Assert.DoesNotContain("version = '0.8.0.0'", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Add-AppxPackage", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Import-Certificate", source, StringComparison.OrdinalIgnoreCase);

        string workflow = File.ReadAllText(Path.Combine(repositoryRoot, ".github", "workflows", "ci.yml"));
        Assert.Contains("actions/setup-dotnet@v6", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("actions/setup-dotnet@v4", workflow, StringComparison.Ordinal);
        Assert.Contains("Run product version tool self-tests", workflow, StringComparison.Ordinal);
        Assert.Contains(".\\dev\\version.tests.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("Build and validate unsigned MSIX mechanism", workflow, StringComparison.Ordinal);
        Assert.Contains("id: unsigned-msix", workflow, StringComparison.Ordinal);
        Assert.Contains(".\\scripts\\test-windows-msix-unsigned.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("StudyReportEvaluator-win-x64.unsigned.test.evidence.json", workflow, StringComparison.Ordinal);
        Assert.Contains("steps.unsigned-msix.outcome == 'success'", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void CI_runs_the_Windows_ZIP_regression_once_and_uploads_only_closed_evidence()
    {
        string repositoryRoot = FindRepositoryRoot();
        string driver = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "scripts",
            "test-windows-zip.ps1"));
        string workflow = File.ReadAllText(Path.Combine(
            repositoryRoot,
            ".github",
            "workflows",
            "ci.yml"));

        Assert.Contains("#Requires -Version 7.4", driver, StringComparison.Ordinal);
        Assert.Contains("#Requires -PSEdition Core", driver, StringComparison.Ordinal);
        Assert.Contains("WindowsPublishPackageTests", driver, StringComparison.Ordinal);
        Assert.Contains("$RequiredTestCount = 3", driver, StringComparison.Ordinal);
        Assert.Contains("must be generated from a clean source checkout", driver, StringComparison.Ordinal);
        Assert.Contains("user-workbook-sentinel.xlsx", driver, StringComparison.Ordinal);
        Assert.Contains("windows-zip-required", driver, StringComparison.Ordinal);
        Assert.Contains("PASS_REQUIRED", driver, StringComparison.Ordinal);
        Assert.Contains("StudyReportEvaluator-win-x64.evidence.json", driver, StringComparison.Ordinal);
        Assert.Contains("sourceStatusEntryCount = 0", driver, StringComparison.Ordinal);
        Assert.Contains("safeLayoutVerified = $true", driver, StringComparison.Ordinal);
        Assert.Contains("apphostLaunchVerified = $true", driver, StringComparison.Ordinal);
        Assert.Contains("inputUnchangedVerified = $true", driver, StringComparison.Ordinal);
        Assert.DoesNotContain("PASS_PRODUCTION", driver, StringComparison.Ordinal);
        Assert.True(
            driver.IndexOf(
                "Remove-Item -LiteralPath $evidencePath -Force",
                StringComparison.Ordinal) <
            driver.IndexOf("$statusBefore =", StringComparison.Ordinal));
        Assert.True(
            driver.LastIndexOf(
                "if (Test-Path -LiteralPath $sentinelRoot)",
                StringComparison.Ordinal) <
            driver.IndexOf(
                "Write-AtomicEvidence -Path $evidencePath",
                StringComparison.Ordinal));

        Assert.Contains("FullyQualifiedName!~StudyReportEvaluator.App.Tests.Packaging.WindowsPublishPackageTests", workflow, StringComparison.Ordinal);
        Assert.Contains("Build and validate Windows ZIP regression", workflow, StringComparison.Ordinal);
        Assert.Contains("id: windows-zip", workflow, StringComparison.Ordinal);
        Assert.Contains(".\\scripts\\test-windows-zip.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("steps.windows-zip.outcome == 'success'", workflow, StringComparison.Ordinal);
        Assert.Contains("windows-zip-regression-${{ github.run_id }}", workflow, StringComparison.Ordinal);
        Assert.Contains("artifacts/package/StudyReportEvaluator-win-x64.zip", workflow, StringComparison.Ordinal);
        Assert.Contains("artifacts/package/StudyReportEvaluator-win-x64.evidence.json", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void Msix_mechanism_script_has_an_explicit_certificate_free_Windows_11_development_mode()
    {
        string repositoryRoot = FindRepositoryRoot();
        string path = Path.Combine(repositoryRoot, "scripts", "package-windows-msix.ps1");
        string source = File.ReadAllText(path);

        Assert.Contains("DefaultParameterSetName = 'SignedTest'", source, StringComparison.Ordinal);
        Assert.Contains("ParameterSetName = 'SignedTest'", source, StringComparison.Ordinal);
        Assert.Contains("ParameterSetName = 'UnsignedDevelopment'", source, StringComparison.Ordinal);
        Assert.Contains("$UnsignedDevelopment", source, StringComparison.Ordinal);
        Assert.Contains("StudyReportEvaluator-win-x64.unsigned.test.msix", source, StringComparison.Ordinal);
        Assert.Contains("OID.2.25.311729368913984317654407730594956997722=1", source, StringComparison.Ordinal);
        Assert.Contains("must be the final Publisher field", source, StringComparison.Ordinal);
        Assert.Contains("Test-ContainsUnsignedPackagePublisherMarker -Value $Publisher", source, StringComparison.Ordinal);
        Assert.Contains("Signed test packages cannot use the unsigned-development Publisher OID identity.", source, StringComparison.Ordinal);
        Assert.Contains("if (-not $isUnsignedDevelopment)", source, StringComparison.Ordinal);
        Assert.Contains("AppxSignature.p7x", source, StringComparison.Ordinal);
        Assert.Contains("elevated Add-AppxPackage -AllowUnsigned", source, StringComparison.Ordinal);
        Assert.Contains("PASS_MECHANISM only", source, StringComparison.Ordinal);
        Assert.Contains("not for distribution", source, StringComparison.Ordinal);
        Assert.Contains("Assert-NoReparsePointsBelowRoot -Root $repositoryRoot -Path $PublishedDirectory", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Import-Certificate", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TrustedPeople", source, StringComparison.OrdinalIgnoreCase);
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

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
