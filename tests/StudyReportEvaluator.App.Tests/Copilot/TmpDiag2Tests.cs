using GitHub.Copilot;
using StudyReportEvaluator.App.Copilot;
using Xunit;

namespace StudyReportEvaluator.App.Tests.Copilot;

public sealed class TmpDiag2Tests
{
    [Fact]
    public async Task Diag2()
    {
        if (Environment.GetEnvironmentVariable("TMP_DIAG") != "1") return;
        var lines = new List<string>();
        var factory = new CopilotClientFactory(new BundledCopilotCliPathResolver(@"C:\GitHub\StudyReport-Evaluator\artifacts\package\publish\win-x64"));
        var created = await factory.CreateAsync(CancellationToken.None);
        CopilotClient client = created.Client!;
        await client.StartAsync();
        var models = await client.ListModelsAsync();
        foreach (var m in models.Where(m => m.Id.Contains("haiku")))
            lines.Add($"{m.Id} effortSupport={m.Capabilities?.Supports?.ReasoningEffort} supported=[{string.Join(",", m.SupportedReasoningEfforts ?? [])}] default={m.DefaultReasoningEffort}");
        try
        {
            var s = await client.CreateSessionAsync(new SessionConfig
            {
                Model = "claude-haiku-5.5",
                ReasoningEffort = "low",
                OnPermissionRequest = (_, _) => Task.FromResult(PermissionDecision.Reject("no")),
            });
            var r = await s.SendAndWaitAsync(new MessageOptions { Prompt = "Reply OK" });
            lines.Add("OK " + r?.Data?.Content);
        }
        catch (Exception e) { lines.Add("EX " + e.GetType().Name + ": " + e.Message); }
        File.WriteAllLines(Path.Combine(Path.GetTempPath(), "tmpdiag2.txt"), lines);
    }
}
