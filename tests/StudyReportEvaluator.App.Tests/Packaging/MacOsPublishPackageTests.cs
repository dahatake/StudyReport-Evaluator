using System.Xml.Linq;
using Xunit;

namespace StudyReportEvaluator.App.Tests.Packaging;

public sealed class MacOsPublishPackageTests
{
    [Fact]
    public void MacOs_manifest_and_entitlements_preserve_the_minimal_unsigned_bundle_contract()
    {
        string root = FindRepositoryRoot();
        string plistPath = Path.Combine(root, "eng", "packaging", "macos", "Info.plist");
        string entitlementsPath = Path.Combine(root, "eng", "packaging", "macos", "StudyReportEvaluator.entitlements");
        XDocument plist = XDocument.Load(plistPath);
        XDocument entitlements = XDocument.Load(entitlementsPath);

        XElement plistDictionary = Assert.Single(plist.Root!.Elements("dict"));
        Dictionary<string, XElement> properties = ToPlistDictionary(plistDictionary);
        Assert.Equal("StudyReportEvaluator.App", properties["CFBundleExecutable"].Value);
        Assert.Equal("com.github.dahatake.study-report-evaluator", properties["CFBundleIdentifier"].Value);
        Assert.Equal("StudyReportEvaluator.icns", properties["CFBundleIconFile"].Value);
        Assert.Equal("@PRODUCT_VERSION@", properties["CFBundleShortVersionString"].Value);
        Assert.Equal("@BUILD_VERSION@", properties["CFBundleVersion"].Value);
        Assert.Equal("14.0", properties["LSMinimumSystemVersion"].Value);

        XElement entitlementDictionary = Assert.Single(entitlements.Root!.Elements("dict"));
        Assert.Empty(entitlementDictionary.Elements("key"));
        Assert.DoesNotContain("com.apple.security.get-task-allow", entitlements.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("com.apple.security.cs.allow-unsigned-executable-memory", entitlements.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("com.apple.security.cs.disable-library-validation", entitlements.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void MacOs_scripts_keep_mechanism_and_production_evidence_separate()
    {
        string root = FindRepositoryRoot();
        string publish = File.ReadAllText(Path.Combine(root, "scripts", "publish-macos.sh"));
        string package = File.ReadAllText(Path.Combine(root, "scripts", "package-macos.sh"));
        string sign = File.ReadAllText(Path.Combine(root, "scripts", "sign-macos.sh"));
        string notary = File.ReadAllText(Path.Combine(root, "scripts", "notarize-package-macos.sh"));

        foreach (string script in new[] { publish, package, sign, notary })
        {
            Assert.Contains("set -euo pipefail", script, StringComparison.Ordinal);
            Assert.Contains("osx-arm64", script, StringComparison.Ordinal);
            Assert.Contains("osx-x64", script, StringComparison.Ordinal);
            Assert.Contains("uname -s", script, StringComparison.Ordinal);
        }

        Assert.Contains("assert_thin_macho_executable", publish, StringComparison.Ordinal);
        Assert.Contains("assert_thin_macho_library", publish, StringComparison.Ordinal);
        Assert.Contains("copilot-source-provenance.json", package, StringComparison.Ordinal);
        Assert.Contains("Production signing/notarization status: NOT_RUN", package, StringComparison.Ordinal);
        Assert.DoesNotContain("sign-macos.sh", package, StringComparison.Ordinal);
        Assert.DoesNotContain("notarize-package-macos.sh", package, StringComparison.Ordinal);
        Assert.Contains("--options runtime", sign, StringComparison.Ordinal);
        Assert.DoesNotContain("codesign --deep", sign, StringComparison.Ordinal);
        Assert.Contains("Pre-notarization Gatekeeper assessment", sign, StringComparison.Ordinal);
        Assert.Contains("notarytool submit", notary, StringComparison.Ordinal);
        Assert.Contains("notarytool log", notary, StringComparison.Ordinal);
        Assert.Contains("stapler staple", notary, StringComparison.Ordinal);
        Assert.Contains("stapler validate", notary, StringComparison.Ordinal);
        Assert.Contains("notary log contains issues", notary, StringComparison.Ordinal);
        Assert.Contains("--keychain-profile", notary, StringComparison.Ordinal);
        Assert.DoesNotContain("--apple-id", notary, StringComparison.Ordinal);
        Assert.DoesNotContain("--password", notary, StringComparison.Ordinal);
    }

    private static Dictionary<string, XElement> ToPlistDictionary(XElement dictionary)
    {
        XElement[] elements = dictionary.Elements().ToArray();
        Dictionary<string, XElement> result = new(StringComparer.Ordinal);
        for (int index = 0; index < elements.Length; index += 2)
        {
            Assert.Equal("key", elements[index].Name.LocalName);
            Assert.True(index + 1 < elements.Length);
            Assert.True(result.TryAdd(elements[index].Value, elements[index + 1]));
        }

        return result;
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
