namespace StudyReportEvaluator.App.Copilot;

public sealed class SystemCopilotCliPathResolver : ICopilotCliPathResolver
{
    private readonly string? searchPath;

    public SystemCopilotCliPathResolver()
        : this(Environment.GetEnvironmentVariable("PATH"))
    {
    }

    public SystemCopilotCliPathResolver(string? searchPath)
    {
        this.searchPath = searchPath;
    }

    public ValueTask<string?> ResolveAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string executableName = OperatingSystem.IsWindows() ? "copilot.exe" : "copilot";
        foreach (string entry in (searchPath ?? string.Empty).Split(Path.PathSeparator))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string directory = entry.Trim().Trim('"');
            // Empty and relative entries must not search the working directory.
            if (!Path.IsPathFullyQualified(directory))
            {
                continue;
            }

            string candidate = Path.Combine(directory, executableName);
            if (File.Exists(candidate))
            {
                if (!OperatingSystem.IsWindows()
                    && (File.GetUnixFileMode(candidate)
                        & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) == 0)
                {
                    continue;
                }

                return ValueTask.FromResult<string?>(Path.GetFullPath(candidate));
            }
        }

        return ValueTask.FromResult<string?>(null);
    }
}
