using System.Security.Cryptography;
using StudyReportEvaluator.App.Copilot;
using Xunit;

namespace StudyReportEvaluator.App.Tests.Copilot;

public sealed class SystemCopilotCliPathResolverTests : IDisposable
{
    private readonly string directory = Path.Combine(
        Path.GetTempPath(), "StudyReportEvaluator-SystemCli-" + Guid.NewGuid().ToString("N"));

    public SystemCopilotCliPathResolverTests() => Directory.CreateDirectory(directory);

    [Fact]
    public async Task Resolves_installed_native_cli_in_PATH_order_without_a_bundle_manifest_AC020()
    {
        string first = CreateCli("first directory");
        string second = CreateCli("second directory");
        SystemCopilotCliPathResolver resolver = new(string.Join(
            Path.PathSeparator, Path.GetDirectoryName(first), Path.GetDirectoryName(second)));

        Assert.Equal(first, await resolver.ResolveAsync(TestContext.Current.CancellationToken));
        Assert.False(File.Exists(Path.Combine(directory, BundledCopilotCliPathResolver.ManifestFileName)));

        CopilotClientFactory factory = new(resolver);
        CopilotClientCreationResult result = await factory.CreateAsync(TestContext.Current.CancellationToken);
        Assert.Equal(CopilotClientCreationStatus.Created, result.Status);
        Assert.Equal(first, result.Identity?.CliPath);
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(
                first, TestContext.Current.CancellationToken))),
            result.Identity?.CliSha256);
        await result.Client!.DisposeAsync();
    }

    [Fact]
    public async Task Skips_empty_relative_missing_entries_and_accepts_quoted_absolute_directory_AC020()
    {
        string cli = CreateCli("directory with spaces");
        SystemCopilotCliPathResolver resolver = new(string.Join(
            Path.PathSeparator, "", ".", "relative", Path.Combine(directory, "missing"),
            "\"" + Path.GetDirectoryName(cli) + "\""));

        Assert.Equal(cli, await resolver.ResolveAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("relative")]
    public async Task Missing_system_cli_is_unavailable_without_bundled_fallback_AC023(string? searchPath)
    {
        CreateCli("runtimes");
        CopilotClientFactory factory = new(new SystemCopilotCliPathResolver(searchPath));

        CopilotClientCreationResult result = await factory.CreateAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CopilotClientCreationStatus.CliUnavailable, result.Status);
        Assert.Null(result.Client);
    }

    [Fact]
    public async Task Does_not_select_command_scripts_instead_of_native_windows_cli_AC020()
    {
        await File.WriteAllTextAsync(
            Path.Combine(directory, "copilot.cmd"), "not executable",
            TestContext.Current.CancellationToken);

        Assert.Null(await new SystemCopilotCliPathResolver(directory)
            .ResolveAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Cancellation_prevents_discovery_AC023()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await new SystemCopilotCliPathResolver(directory).ResolveAsync(cancellation.Token));
    }

    public static bool RunInstalledCliCheck =>
        Environment.GetEnvironmentVariable("STUDY_REPORT_EVALUATOR_TEST_SYSTEM_CLI") == "1";

    [Fact(Skip = "Set STUDY_REPORT_EVALUATOR_TEST_SYSTEM_CLI=1 to check the installed CLI.",
        SkipUnless = nameof(RunInstalledCliCheck))]
    public async Task Installed_cli_supports_sdk_authentication_and_model_discovery_without_AI_AC033()
    {
        CopilotAuthenticationService service = new(new CopilotClientFactory(), TimeSpan.FromSeconds(45));

        CopilotAuthenticationResult result = await service.CheckAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CopilotAuthenticationStatus.Available, result.Status);
        Assert.NotEmpty(result.AvailableModels);
        Assert.NotNull(result.Identity);
        Assert.Equal(
            await new SystemCopilotCliPathResolver().ResolveAsync(TestContext.Current.CancellationToken),
            result.Identity.CliPath);
    }

    public void Dispose() => Directory.Delete(directory, recursive: true);

    private string CreateCli(string subdirectory)
    {
        string parent = Path.Combine(directory, subdirectory);
        Directory.CreateDirectory(parent);
        string path = Path.Combine(parent, OperatingSystem.IsWindows() ? "copilot.exe" : "copilot");
        File.Copy(typeof(CopilotClientFactory).Assembly.Location, path);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        return path;
    }
}
