using System.Globalization;
using StudyReportEvaluator.App.Logging;
using StudyReportEvaluator.App.Workbooks.Writing;
using StudyReportEvaluator.App.Workflow;
using Xunit;

namespace StudyReportEvaluator.App.Tests.Logging;

public sealed class RunTextLogTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "SRE-RunTextLog-" + Guid.NewGuid().ToString("N"));

    public RunTextLogTests() => Directory.CreateDirectory(directory);

    public void Dispose()
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    // FR-108 AC-062: {yyyyMMddHHmm}-log.txt, then -02 without overwriting.
    [Fact]
    public void FR108_AC062_saves_with_sequence_and_never_overwrites()
    {
        RunTextLog first = new(TimeProvider.System) { Directory = directory };
        Assert.Equal(RunTextLog.Saved, first.Save());
        string existing = Assert.Single(Directory.GetFiles(directory, "*-log.txt"));
        string before = File.ReadAllText(existing);

        RunTextLog second = new(TimeProvider.System) { Directory = directory };
        Assert.Equal(RunTextLog.Saved, second.Save());
        Assert.Equal(2, Directory.GetFiles(directory, "*-log.txt").Length);
        Assert.Equal(before, File.ReadAllText(existing));
        Assert.Matches(@"^\d{12}(-02)?-log\.txt$", Path.GetFileName(existing));
    }

    // FR-108 AC-063: only numbers/closed codes; failure is a closed code without exception.
    [Fact]
    public void FR108_AC063_records_only_closed_progress_and_reports_failure_as_code()
    {
        RunTextLog log = new(TimeProvider.System) { Directory = directory };
        log.AddProgress(new DurableEvaluationProgress(DurableEvaluationStage.EvaluatingRows, 1, 1, 0, 2, 1, 5, 1, "EVALUATING_ROW"));
        Assert.Equal(RunTextLog.Saved, log.Save());
        string text = File.ReadAllText(Assert.Single(Directory.GetFiles(directory, "*-log.txt")));
        Assert.Contains("stage=EvaluatingRows status=EVALUATING_ROW reference=1/1 row=0/2 operation=1/5 inFlight=1", text, StringComparison.Ordinal);
        Assert.DoesNotContain(directory, text, StringComparison.OrdinalIgnoreCase);

        string blockerFile = Path.Combine(directory, "blocker");
        File.WriteAllText(blockerFile, "x");
        RunTextLog failing = new(TimeProvider.System) { Directory = Path.Combine(blockerFile, "sub") };
        Assert.Equal(RunTextLog.WriteFailed, failing.Save());
        Assert.Equal(RunTextLog.NotAttempted, new RunTextLog(TimeProvider.System).Save());
    }

    // FR-107 AC-060: next unused eval-yyyyMMdd-HHmm[-NN].xlsx name.
    [Fact]
    public void FR107_AC060_FindUnusedFinalPath_skips_existing_final_and_partial_names()
    {
        DateTimeOffset now = new(2026, 11, 3, 21, 36, 0, TimeSpan.FromHours(9));
        Assert.Equal("eval-20261103-2136.xlsx", Path.GetFileName(OutputPathPlanner.FindUnusedFinalPath(directory, now)));
        File.WriteAllText(Path.Combine(directory, "eval-20261103-2136.xlsx"), "a");
        Assert.Equal("eval-20261103-2136-02.xlsx", Path.GetFileName(OutputPathPlanner.FindUnusedFinalPath(directory, now)));
        File.WriteAllText(Path.Combine(directory, "eval-20261103-2136-02.partial.xlsx"), "a");
        string next = OutputPathPlanner.FindUnusedFinalPath(directory, now);
        Assert.Equal("eval-20261103-2136-03.xlsx", Path.GetFileName(next));
        Assert.False(File.Exists(next));
        _ = CultureInfo.InvariantCulture;
    }
}
