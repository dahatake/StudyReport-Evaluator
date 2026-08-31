using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Security;
using System.Security.Cryptography;
using GitHub.Copilot;

namespace StudyReportEvaluator.App.Copilot;

public interface ICopilotCliPathResolver
{
    ValueTask<string?> ResolveAsync(CancellationToken cancellationToken);
}

public sealed class EnvironmentPathCopilotCliPathResolver : ICopilotCliPathResolver
{
    private static readonly string[] WindowsExecutableNames = ["copilot.exe"];
    private static readonly string[] OtherExecutableNames = ["copilot"];

    public ValueTask<string?> ResolveAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string? searchPath = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(searchPath))
        {
            return ValueTask.FromResult<string?>(null);
        }

        string[] executableNames = OperatingSystem.IsWindows()
            ? WindowsExecutableNames
            : OtherExecutableNames;

        foreach (string entry in searchPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            cancellationToken.ThrowIfCancellationRequested();

            string directory = entry.Trim('"');
            if (!Path.IsPathFullyQualified(directory))
            {
                continue;
            }

            foreach (string executableName in executableNames)
            {
                try
                {
                    string candidate = Path.GetFullPath(Path.Combine(directory, executableName));
                    if (File.Exists(candidate))
                    {
                        return ValueTask.FromResult<string?>(candidate);
                    }
                }
                catch (Exception exception) when (IsInvalidPathException(exception))
                {
                    // Ignore malformed PATH entries. No path value is logged or retained.
                }
            }
        }

        return ValueTask.FromResult<string?>(null);
    }

    private static bool IsInvalidPathException(Exception exception) =>
        exception is ArgumentException or NotSupportedException or PathTooLongException;
}

public interface ICopilotClientFactory
{
    ValueTask<CopilotClientCreationResult> CreateAsync(CancellationToken cancellationToken);
}

public enum CopilotClientCreationStatus
{
    Created,
    CliUnavailable,
    RuntimeFailed,
    Cancelled,
}

public sealed class CopilotRuntimeIdentity
{
    public CopilotRuntimeIdentity(
        string cliPath,
        string cliVersion,
        string cliSha256,
        string sdkInformationalVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cliPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(cliVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(cliSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(sdkInformationalVersion);

        if (!Path.IsPathFullyQualified(cliPath))
        {
            throw new ArgumentException("The CLI path must be absolute.", nameof(cliPath));
        }

        if (!IsSafeVersion(cliVersion))
        {
            throw new ArgumentException("The CLI version is not a safe identity value.", nameof(cliVersion));
        }

        if (cliSha256.Length != 64 || cliSha256.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("The CLI SHA-256 must contain exactly 64 hexadecimal characters.", nameof(cliSha256));
        }

        if (!IsSafeVersion(sdkInformationalVersion))
        {
            throw new ArgumentException("The SDK informational version is not a safe identity value.", nameof(sdkInformationalVersion));
        }

        CliPath = Path.GetFullPath(cliPath);
        CliVersion = cliVersion;
        CliSha256 = cliSha256.ToUpperInvariant();
        SdkInformationalVersion = sdkInformationalVersion;
    }

    public string CliPath { get; }

    public string CliVersion { get; }

    public string CliSha256 { get; }

    public string SdkInformationalVersion { get; }

    public override string ToString() =>
        $"CLI version {CliVersion}, CLI SHA-256 {CliSha256}, SDK {SdkInformationalVersion}";

    internal static bool IsSafeVersion(string value) =>
        value.Length is > 0 and <= 128
        && char.IsAsciiDigit(value[0])
        && value.All(character =>
            char.IsAsciiLetterOrDigit(character)
            || character is '.' or '-' or '+' or '_');
}

public sealed class CopilotClientCreationResult
{
    private CopilotClientCreationResult(
        CopilotClientCreationStatus status,
        CopilotClient? client,
        CopilotClientOptions? options,
        CopilotRuntimeIdentity? identity)
    {
        Status = status;
        Client = client;
        Options = options;
        Identity = identity;
    }

    public CopilotClientCreationStatus Status { get; }

    public CopilotClient? Client { get; }

    public CopilotClientOptions? Options { get; }

    public CopilotRuntimeIdentity? Identity { get; }

    public override string ToString() =>
        Identity is null ? Status.ToString() : $"{Status}: {Identity}";

    internal static CopilotClientCreationResult Created(
        CopilotClient client,
        CopilotClientOptions options,
        CopilotRuntimeIdentity identity) =>
        new(CopilotClientCreationStatus.Created, client, options, identity);

    internal static CopilotClientCreationResult Failed(CopilotClientCreationStatus status)
    {
        if (status == CopilotClientCreationStatus.Created)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        return new CopilotClientCreationResult(status, null, null, null);
    }
}

public sealed class CopilotClientFactory : ICopilotClientFactory
{
    private readonly ICopilotCliPathResolver _pathResolver;

    public CopilotClientFactory()
        : this(new EnvironmentPathCopilotCliPathResolver())
    {
    }

    public CopilotClientFactory(ICopilotCliPathResolver pathResolver)
    {
        ArgumentNullException.ThrowIfNull(pathResolver);
        _pathResolver = pathResolver;
    }

    public async ValueTask<CopilotClientCreationResult> CreateAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return CopilotClientCreationResult.Failed(CopilotClientCreationStatus.Cancelled);
        }

        string? resolvedPath;
        try
        {
            resolvedPath = await _pathResolver.ResolveAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return CopilotClientCreationResult.Failed(CopilotClientCreationStatus.Cancelled);
        }
        catch
        {
            return CopilotClientCreationResult.Failed(CopilotClientCreationStatus.RuntimeFailed);
        }

        if (string.IsNullOrWhiteSpace(resolvedPath) || !Path.IsPathFullyQualified(resolvedPath))
        {
            return CopilotClientCreationResult.Failed(CopilotClientCreationStatus.CliUnavailable);
        }

        CopilotRuntimeIdentity identity;
        try
        {
            identity = await ReadIdentityAsync(Path.GetFullPath(resolvedPath), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return CopilotClientCreationResult.Failed(CopilotClientCreationStatus.Cancelled);
        }
        catch (Exception exception) when (IsCliUnavailableException(exception))
        {
            return CopilotClientCreationResult.Failed(CopilotClientCreationStatus.CliUnavailable);
        }
        catch
        {
            return CopilotClientCreationResult.Failed(CopilotClientCreationStatus.RuntimeFailed);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return CopilotClientCreationResult.Failed(CopilotClientCreationStatus.Cancelled);
        }

        CopilotClientOptions options = BuildOptions(identity.CliPath);
        try
        {
            CopilotClient client = new(options);
            return CopilotClientCreationResult.Created(client, options, identity);
        }
        catch
        {
            return CopilotClientCreationResult.Failed(CopilotClientCreationStatus.RuntimeFailed);
        }
    }

    private static CopilotClientOptions BuildOptions(string cliPath) =>
        new()
        {
            Connection = RuntimeConnection.ForStdio(path: cliPath),
            Mode = CopilotClientMode.CopilotCli,
            UseLoggedInUser = true,
            GitHubToken = null,
            LogLevel = CopilotLogLevel.None,
            Logger = null,
            Telemetry = null,
            OnListModels = null,
            EnableRemoteSessions = false,
        };

    private static async ValueTask<CopilotRuntimeIdentity> ReadIdentityAsync(
        string cliPath,
        CancellationToken cancellationToken)
    {
        FileInfo before = new(cliPath);
        before.Refresh();
        if (!before.Exists || before.Length == 0)
        {
            throw new FileNotFoundException("The Copilot CLI executable is unavailable.");
        }

        string? cliVersion = ReadCliVersion(cliPath);
        if (cliVersion is null)
        {
            throw new InvalidDataException("The Copilot CLI version cannot be verified.");
        }

        byte[] hash;
        await using (FileStream stream = new(
            cliPath,
            new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.Read,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
            }))
        {
            hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        }

        FileInfo after = new(cliPath);
        after.Refresh();
        if (!after.Exists
            || before.Length != after.Length
            || before.LastWriteTimeUtc != after.LastWriteTimeUtc)
        {
            throw new IOException("The Copilot CLI executable changed while its identity was being read.");
        }

        string? sdkInformationalVersion = typeof(CopilotClient).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        if (sdkInformationalVersion is null || !CopilotRuntimeIdentity.IsSafeVersion(sdkInformationalVersion))
        {
            throw new InvalidDataException("The Copilot SDK informational version cannot be verified.");
        }

        return new CopilotRuntimeIdentity(
            cliPath,
            cliVersion,
            Convert.ToHexString(hash),
            sdkInformationalVersion);
    }

    private static string? ReadCliVersion(string cliPath)
    {
        FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(cliPath);
        foreach (string? candidate in new[] { versionInfo.ProductVersion, versionInfo.FileVersion })
        {
            if (candidate is not null && CopilotRuntimeIdentity.IsSafeVersion(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool IsCliUnavailableException(Exception exception) =>
        exception is IOException
            or UnauthorizedAccessException
            or SecurityException
            or Win32Exception
            or ArgumentException
            or NotSupportedException;
}
