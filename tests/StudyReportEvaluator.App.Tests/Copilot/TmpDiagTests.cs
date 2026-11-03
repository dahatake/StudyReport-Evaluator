using StudyReportEvaluator.App.Copilot;
using StudyReportEvaluator.Core.Domain;
using StudyReportEvaluator.Core.Prompting;
using Xunit;

namespace StudyReportEvaluator.App.Tests.Copilot;

public sealed class TmpDiagTests
{
    [Fact]
    public async Task Diag()
    {
        if (Environment.GetEnvironmentVariable("TMP_DIAG") != "1") return;
        var lines = new List<string>();
        foreach ((string model, string? effort) in new (string, string?)[]
        {
            ("claude-haiku-5.5", "low"),
            ("claude-haiku-5.5", null),
            ("auto", null),
        })
        {
            EphemeralEvaluationRunner runner = new(
                new SdkEphemeralCopilotTransportFactory(new CopilotClientFactory(
                    new BundledCopilotCliPathResolver(@"C:\GitHub\StudyReport-Evaluator\artifacts\package\publish\win-x64"))));
            EphemeralEvaluationResult r = await runner.EvaluateAsync(Payload(), model, effort, TestContext.Current.CancellationToken);
            lines.Add($"{model}/{effort}: {r.StatusCode} attempts={r.AttemptCount}");
        }
        File.WriteAllLines(Path.Combine(Path.GetTempPath(), "tmpdiag.txt"), lines);
    }

    private static SafeEvaluationPayload Payload() => new(
        "q", "e",
        "Call submit_quantification exactly once with criterion c, score 1, a short reason, evidence 'abc', PRIMARY_ANSWER, and source column G.",
        new EvaluationSourceCell(EvaluationSourceKind.PrimaryAnswer, "G", "abc"),
        [],
        [new ExpectedCriterion("c", "C", new ScoreRange(0m, 1m))]);
}
