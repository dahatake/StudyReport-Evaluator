using System.Diagnostics;

namespace StudyReportEvaluator.App.Tests.Packaging;

/// <summary>
/// Resolves a real PowerShell 7+ executable. Microsoft Store installs expose pwsh.exe only as an
/// app-execution alias under WindowsApps, whose file version resource reads as 0.0, so the version
/// is asked from the executable itself when the file metadata is unusable.
/// </summary>
internal static class PowerShellCoreLocator
{
    internal static string? TryFind()
    {
        HashSet<string> candidates = new(StringComparer.OrdinalIgnoreCase);
        foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                     .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            candidates.Add(Path.Combine(directory.Trim('"'), "pwsh.exe"));
        }

        string installedRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "PowerShell");
        if (Directory.Exists(installedRoot))
        {
            foreach (string directory in Directory.EnumerateDirectories(installedRoot))
            {
                candidates.Add(Path.Combine(directory, "pwsh.exe"));
            }
        }

        return candidates
            .Where(File.Exists)
            .Select(path => (Path: Path.GetFullPath(path), Version: ResolveVersion(path)))
            .Where(candidate => candidate.Version is { Major: >= 7 })
            .OrderByDescending(candidate => candidate.Version)
            .Select(candidate => candidate.Path)
            .FirstOrDefault();
    }

    internal static string Find(string failureMessage) =>
        TryFind() ?? throw new FileNotFoundException(failureMessage);

    private static Version? ResolveVersion(string path)
    {
        FileVersionInfo info = FileVersionInfo.GetVersionInfo(path);
        if (info.FileMajorPart >= 7)
        {
            return new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart, info.FilePrivatePart);
        }

        try
        {
            ProcessStartInfo start = new()
            {
                FileName = path,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            start.ArgumentList.Add("-NoLogo");
            start.ArgumentList.Add("-NoProfile");
            start.ArgumentList.Add("-NonInteractive");
            start.ArgumentList.Add("-Command");
            start.ArgumentList.Add("$PSVersionTable.PSVersion.ToString()");
            using Process process = new() { StartInfo = start };
            if (!process.Start())
            {
                return null;
            }

            Task<string> output = process.StandardOutput.ReadToEndAsync();
            _ = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(15_000))
            {
                process.Kill(entireProcessTree: true);
                return null;
            }

            string text = output.GetAwaiter().GetResult().Trim();
            int dash = text.IndexOf('-', StringComparison.Ordinal);
            return process.ExitCode == 0 && Version.TryParse(dash > 0 ? text[..dash] : text, out Version? version)
                ? version
                : null;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
                or System.ComponentModel.Win32Exception
                or IOException)
        {
            return null;
        }
    }
}
