namespace StudyReportEvaluator.App.Usage;

/// <summary>Closed job metadata; never retains arbitrary runtime or model text.</summary>
public sealed class JobUsageContext
{
    public JobUsageContext(string? applicationVersion = null, string? sdkVersion = null,
        string? cliVersion = null, string? requestedModelKey = null,
        int? maxConcurrency = null, bool? isResume = null, bool? requestedModelIsAuto = null)
    {
        ApplicationVersion = VersionNumber(applicationVersion);
        SdkVersion = VersionNumber(sdkVersion);
        CliVersion = VersionNumber(cliVersion);
        RequestedModelKey = requestedModelKey is null ? null : ModelUsageSnapshot.SafeKey(requestedModelKey);
        RequestedModelIsAuto = requestedModelIsAuto ?? (requestedModelKey is null ? null : requestedModelKey == "auto");
        MaxConcurrency = maxConcurrency is >= 1 and <= 3 ? maxConcurrency : null;
        IsResume = isResume;
    }

    public string? ApplicationVersion { get; }
    public string? SdkVersion { get; }
    public string? CliVersion { get; }
    public string? RequestedModelKey { get; }
    public bool? RequestedModelIsAuto { get; }
    public int? MaxConcurrency { get; }
    public bool? IsResume { get; }

    private static string? VersionNumber(string? value)
    {
        if (value is null || value.Length > 256) return null;
        string number = value.Split(['-', '+'], 2)[0];
        return number.Length > 0 && number.All(c => char.IsAsciiDigit(c) || c == '.')
            && Version.TryParse(number, out var version) ? version.ToString() : null;
    }
}