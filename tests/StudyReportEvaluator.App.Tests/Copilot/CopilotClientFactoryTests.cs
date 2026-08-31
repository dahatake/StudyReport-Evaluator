using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using GitHub.Copilot;
using StudyReportEvaluator.App.Copilot;
using Xunit;

namespace StudyReportEvaluator.App.Tests.Copilot;

public sealed class CopilotClientFactoryTests
{
    [Fact]
    public async Task Factory_uses_explicit_cli_path_existing_login_and_no_secret_or_telemetry()
    {
        string cliPath = typeof(CopilotClientFactoryTests).Assembly.Location;
        CopilotClientFactory factory = new(new StubPathResolver(cliPath));

        CopilotClientCreationResult result = await factory.CreateAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CopilotClientCreationStatus.Created, result.Status);
        CopilotClient client = Assert.IsType<CopilotClient>(result.Client);
        CopilotClientOptions options = Assert.IsType<CopilotClientOptions>(result.Options);
        StdioRuntimeConnection connection = Assert.IsType<StdioRuntimeConnection>(options.Connection);
        Assert.Equal(Path.GetFullPath(cliPath), connection.Path);
        Assert.True(connection.Args is null or { Count: 0 });
        Assert.Equal(CopilotClientMode.CopilotCli, options.Mode);
        Assert.True(options.UseLoggedInUser);
        Assert.Null(options.GitHubToken);
        Assert.Equal(CopilotLogLevel.None, options.LogLevel);
        Assert.Null(options.Logger);
        Assert.Null(options.Telemetry);
        Assert.Null(options.Environment);
        Assert.Null(GetOptionValue(options, "OnGitHubTelemetry"));
        Assert.Null(GetOptionValue(options, "RequestHandler"));
        Assert.Null(options.OnListModels);
        Assert.False(options.EnableRemoteSessions);
        Assert.Null(connection.Environment);

        await client.DisposeAsync();
    }

    [Fact]
    public async Task Factory_returns_exact_cli_and_sdk_identity_without_exposing_path_in_safe_text()
    {
        string cliPath = typeof(CopilotClientFactoryTests).Assembly.Location;
        CopilotClientFactory factory = new(new StubPathResolver(cliPath));
        string expectedHash;
        await using (FileStream stream = File.OpenRead(cliPath))
        {
            expectedHash = Convert.ToHexString(await SHA256.HashDataAsync(
                stream,
                TestContext.Current.CancellationToken));
        }

        FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(cliPath);
        string expectedCliVersion = versionInfo.ProductVersion ?? versionInfo.FileVersion
            ?? throw new InvalidOperationException("The test assembly has no file version.");
        string expectedSdkVersion = typeof(CopilotClient).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? throw new InvalidOperationException("The SDK has no informational version.");

        CopilotClientCreationResult result = await factory.CreateAsync(TestContext.Current.CancellationToken);

        CopilotRuntimeIdentity identity = Assert.IsType<CopilotRuntimeIdentity>(result.Identity);
        Assert.Equal(Path.GetFullPath(cliPath), identity.CliPath);
        Assert.Equal(expectedCliVersion, identity.CliVersion);
        Assert.Equal(expectedHash, identity.CliSha256);
        Assert.Equal(expectedSdkVersion, identity.SdkInformationalVersion);
        Assert.DoesNotContain(cliPath, identity.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(cliPath, result.ToString(), StringComparison.OrdinalIgnoreCase);

        CopilotClient client = Assert.IsType<CopilotClient>(result.Client);
        await client.DisposeAsync();
    }

    [Fact]
    public async Task Missing_cli_maps_to_unavailable_without_constructing_a_bundled_client()
    {
        CopilotClientFactory factory = new(new StubPathResolver(null));

        CopilotClientCreationResult result = await factory.CreateAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CopilotClientCreationStatus.CliUnavailable, result.Status);
        Assert.Null(result.Client);
        Assert.Null(result.Options);
        Assert.Null(result.Identity);
    }

    [Fact]
    public async Task Relative_cli_path_is_rejected_instead_of_using_process_working_directory()
    {
        CopilotClientFactory factory = new(new StubPathResolver("copilot.exe"));

        CopilotClientCreationResult result = await factory.CreateAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CopilotClientCreationStatus.CliUnavailable, result.Status);
        Assert.Null(result.Client);
    }

    [Fact]
    public async Task Cancellation_is_closed_and_does_not_attempt_path_discovery()
    {
        StubPathResolver resolver = new(typeof(CopilotClientFactoryTests).Assembly.Location);
        CopilotClientFactory factory = new(resolver);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        CopilotClientCreationResult result = await factory.CreateAsync(cancellation.Token);

        Assert.Equal(CopilotClientCreationStatus.Cancelled, result.Status);
        Assert.False(resolver.WasCalled);
        Assert.Null(result.Client);
    }

    [Fact]
    public void Factory_public_api_has_no_oauth_pat_token_or_secret_input()
    {
        string[] parameterNames = typeof(CopilotClientFactory)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .SelectMany(constructor => constructor.GetParameters())
            .Concat(typeof(CopilotClientFactory)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .SelectMany(method => method.GetParameters()))
            .Select(parameter => parameter.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(parameterNames, name =>
            name.Contains("oauth", StringComparison.OrdinalIgnoreCase)
            || name.Equals("pat", StringComparison.OrdinalIgnoreCase)
            || name.Contains("personalAccessToken", StringComparison.OrdinalIgnoreCase)
            || name.Contains("githubToken", StringComparison.OrdinalIgnoreCase)
            || name.Contains("accessToken", StringComparison.OrdinalIgnoreCase)
            || name.Contains("secret", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("C:\\private\\copilot.exe")]
    [InlineData("1.2.3\nforged")]
    [InlineData("release candidate")]
    public void Runtime_identity_rejects_unsafe_version_metadata(string unsafeVersion)
    {
        Assert.Throws<ArgumentException>(() => new CopilotRuntimeIdentity(
            Path.GetFullPath(typeof(CopilotClientFactoryTests).Assembly.Location),
            unsafeVersion,
            new string('A', 64),
            "1.0.11+test"));
    }

    private static object? GetOptionValue(CopilotClientOptions options, string propertyName) =>
        typeof(CopilotClientOptions).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(options);

    private sealed class StubPathResolver(string? path) : ICopilotCliPathResolver
    {
        public bool WasCalled { get; private set; }

        public ValueTask<string?> ResolveAsync(CancellationToken cancellationToken)
        {
            WasCalled = true;
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(path);
        }
    }
}
