using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace StudyReportEvaluator.App.Tests.SupplyChain;

public sealed partial class PackageLockTests
{
    private static readonly string[] ProjectPaths =
    [
        "src/StudyReportEvaluator.Core/StudyReportEvaluator.Core.csproj",
        "src/StudyReportEvaluator.App/StudyReportEvaluator.App.csproj",
        "tests/StudyReportEvaluator.Core.Tests/StudyReportEvaluator.Core.Tests.csproj",
        "tests/StudyReportEvaluator.App.Tests/StudyReportEvaluator.App.Tests.csproj",
    ];

    [Fact]
    public void Sdk_and_central_package_versions_are_exact()
    {
        string root = FindRepositoryRoot();
        using JsonDocument global = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "global.json")));
        Assert.Equal("10.0.400", global.RootElement.GetProperty("sdk").GetProperty("version").GetString());
        Assert.False(global.RootElement.GetProperty("sdk").GetProperty("allowPrerelease").GetBoolean());

        XDocument central = XDocument.Load(Path.Combine(root, "Directory.Packages.props"));
        XElement[] versions = central.Descendants("PackageVersion").ToArray();
        Assert.NotEmpty(versions);
        Assert.Equal(versions.Length, versions.Select(element => (string?)element.Attribute("Include")).Distinct(StringComparer.OrdinalIgnoreCase).Count());

        foreach (XElement item in versions)
        {
            string version = (string?)item.Attribute("Version") ?? string.Empty;
            Assert.Matches(ExactVersion(), version);
        }
    }

    [Fact]
    public void Current_projects_opt_in_to_central_management_without_inline_versions()
    {
        string root = FindRepositoryRoot();
        HashSet<string> centralPackages = XDocument.Load(Path.Combine(root, "Directory.Packages.props"))
            .Descendants("PackageVersion")
            .Select(element => (string?)element.Attribute("Include"))
            .OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (string projectPath in ProjectPaths)
        {
            XDocument project = XDocument.Load(Path.Combine(root, projectPath));
            Assert.Equal("true", project.Descendants("ManagePackageVersionsCentrally").Single().Value);
            Assert.Equal("true", project.Descendants("CentralPackageTransitivePinningEnabled").Single().Value);
            foreach (XElement reference in project.Descendants("PackageReference"))
            {
                string packageName = (string?)reference.Attribute("Include")
                    ?? throw new InvalidDataException($"PackageReference Include is missing in {projectPath}.");
                Assert.Null(reference.Attribute("Version"));
                Assert.Null(reference.Element("Version"));
                Assert.Contains(packageName, centralPackages);
            }
        }
    }

    [Fact]
    public void Every_current_project_has_a_version_two_lock_file_matching_direct_versions()
    {
        string root = FindRepositoryRoot();
        Dictionary<string, string> central = XDocument.Load(Path.Combine(root, "Directory.Packages.props"))
            .Descendants("PackageVersion")
            .ToDictionary(
                element => (string)element.Attribute("Include")!,
                element => (string)element.Attribute("Version")!,
                StringComparer.OrdinalIgnoreCase);

        foreach (string projectPath in ProjectPaths)
        {
            string projectDirectory = Path.GetDirectoryName(Path.Combine(root, projectPath))!;
            string lockPath = Path.Combine(projectDirectory, "packages.lock.json");
            Assert.True(File.Exists(lockPath), $"Missing lock file: {projectPath}");
            using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(lockPath));
            Assert.Equal(2, document.RootElement.GetProperty("version").GetInt32());
            JsonElement dependencies = document.RootElement.GetProperty("dependencies").GetProperty("net10.0");
            foreach (JsonProperty package in dependencies.EnumerateObject())
            {
                if (!package.Value.TryGetProperty("type", out JsonElement type)
                    || !string.Equals(type.GetString(), "Direct", StringComparison.Ordinal))
                {
                    continue;
                }

                Assert.True(central.TryGetValue(package.Name, out string? expected), $"Direct package is not centrally pinned: {package.Name}");
                Assert.Equal(expected, package.Value.GetProperty("resolved").GetString());
                Assert.False(string.IsNullOrWhiteSpace(package.Value.GetProperty("contentHash").GetString()));
            }
        }
    }

    [Fact]
    public void Restore_uses_one_declared_https_source_and_lock_files()
    {
        string root = FindRepositoryRoot();
        XDocument config = XDocument.Load(Path.Combine(root, "NuGet.Config"));
        XElement[] sources = config.Descendants("packageSources").Elements("add").ToArray();
        Assert.Single(sources);
        Assert.StartsWith("https://", (string?)sources[0].Attribute("value"), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("true", XDocument.Load(Path.Combine(root, "Directory.Build.props"))
            .Descendants("RestorePackagesWithLockFile").Single().Value);
    }

    [GeneratedRegex(@"^\d+(?:\.\d+){1,3}$", RegexOptions.CultureInvariant)]
    private static partial Regex ExactVersion();

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

        throw new DirectoryNotFoundException("Repository root containing StudyReportEvaluator.slnx was not found.");
    }
}
