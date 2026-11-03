using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using StudyReportEvaluator.App.Copilot;
using StudyReportEvaluator.App.ViewModels;
using StudyReportEvaluator.App.Workflow;
using Xunit;

namespace StudyReportEvaluator.SystemTests.Integration;

/// <summary>
/// System tests whose expectations come only from the acceptance criteria (FR-107 AC-060/AC-061, FR-108 AC-062/AC-063).
/// Production code is used only to launch a synthetic run (fake AI, synthetic workbook) and to reach the screen model.
/// </summary>
public sealed partial class OutputNamingAndRunLogTests
{
    [GeneratedRegex(@"^eval-\d{8}-\d{4}(-\d{2})?\.xlsx$")]
    private static partial Regex DefaultNamePattern();

    [GeneratedRegex(@"^\d{12}-log\.txt$")]
    private static partial Regex LogNamePattern();

    [GeneratedRegex(@"^\d{12}-02-log\.txt$")]
    private static partial Regex LogNameSecondPattern();

    private static string Sha(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static CopilotRuntimeIdentity RuntimeIdentity() =>
        new(Path.Combine(Path.GetTempPath(), "copilot-st-test.exe"), "1.0.82", new string('B', 64), "1.0.11");

    private static ResultsOutputViewModel OpenResults(SyntheticRun run, RunSummary summary) =>
        new(
            new ResultsOutputBoundary(),
            new ExecutionRunContext(
                summary,
                run.InputPath,
                "model-test",
                new CopilotRuntimeIdentity(
                    Path.Combine(Path.GetTempPath(), "copilot-st-test.exe"),
                    "1.0.82",
                    new string('B', 64),
                    "1.0.11")));

    private static void AssertUnusedDefault(ResultsOutputViewModel viewModel, string finalPath, string minuteOfFinal)
    {
        string path = viewModel.OutputPath;
        string name = Path.GetFileName(path);
        Assert.Matches(DefaultNamePattern(), name);
        Assert.NotEqual(Path.GetFullPath(finalPath), Path.GetFullPath(path), StringComparer.OrdinalIgnoreCase);
        Assert.False(File.Exists(path), "既定名は未使用でなければならない");
        Assert.Equal(
            Path.GetDirectoryName(Path.GetFullPath(finalPath)),
            Path.GetDirectoryName(Path.GetFullPath(path)),
            StringComparer.OrdinalIgnoreCase);
        // 自動出力済み final と同じ分なら、-02（既存）を飛ばして -03 以降になる。
        if (name.StartsWith("eval-" + minuteOfFinal, StringComparison.Ordinal))
        {
            Match sequence = Regex.Match(name, @"-(\d{2})\.xlsx$");
            Assert.True(sequence.Success && int.Parse(sequence.Groups[1].Value, CultureInfo.InvariantCulture) >= 3, name);
        }

        Assert.True(viewModel.IsOutputPathValid);
        Assert.False(viewModel.OutputTargetExists);
    }

    // FR-107 AC-060 AC-061: 既存の final（と同分の -02）がある run 完了直後・再度開いたときの既定名は未使用名で、
    // そのまま出力でき、既存 file は変わらない。明示指定した既存名は従来どおり拒否される。
    [Fact]
    public async Task FR107_AC060_AC061_default_alias_workbook_name_is_unused_after_run_and_on_reopen()
    {
        using SyntheticRun run = new();
        RunSummary summary = await run.RunAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(summary.IsDurable);
        string finalPath = Assert.IsType<string>(summary.FinalPath);
        Assert.True(File.Exists(finalPath));
        string finalName = Path.GetFileNameWithoutExtension(finalPath);
        string minute = Regex.Match(finalName, @"^eval-(\d{8}-\d{4})").Groups[1].Value;
        Assert.NotEmpty(minute);
        string secondPath = Path.Combine(Path.GetDirectoryName(finalPath)!, $"eval-{minute}-02.xlsx");
        File.WriteAllText(secondPath, "EXISTING-SECOND");
        string finalHash = Sha(finalPath);
        string secondHash = Sha(secondPath);

        // run 完了直後の結果画面
        ResultsOutputViewModel viewModel = OpenResults(run, summary);
        AssertUnusedDefault(viewModel, finalPath, minute);

        // 同じ結果を再度開く（別の画面モデル・同じモデルへの再読込の両方）
        ResultsOutputViewModel reopened = OpenResults(run, summary);
        AssertUnusedDefault(reopened, finalPath, minute);
        reopened.Load(new ExecutionRunContext(
            summary,
            run.InputPath,
            "model-test",
            RuntimeIdentity()));
        AssertUnusedDefault(reopened, finalPath, minute);

        // 利用者が明示指定した既存名は拒否される（異常系）
        reopened.OutputPath = finalPath;
        Assert.False(reopened.IsOutputPathValid);
        Assert.True(reopened.OutputTargetExists);
        Assert.Equal("完成名が既に存在します。上書きせず、別の名前を指定してください。", reopened.OutputPathStatusText);
        reopened.OutputPath = secondPath;
        Assert.False(reopened.IsOutputPathValid);

        // 既定名のまま検証して出力でき、既存 file は変更されない
        string chosen = viewModel.OutputPath;
        await viewModel.ExportAsync(TestContext.Current.CancellationToken);
        Assert.True(File.Exists(chosen), "既定名で出力できる");
        Assert.Equal(finalHash, Sha(finalPath));
        Assert.Equal(secondHash, Sha(secondPath));
    }

    // FR-108 AC-062 AC-063: ジョブ実行ごとに {yyyyMMddHHmm}-log.txt が 1 件でき、同名があれば -02。
    // 秘匿対象の固定文字列は一切含まれず、ログ保存に失敗しても採点・自動出力は成功する。
    [Fact]
    public async Task FR108_AC062_AC063_run_log_text_is_saved_with_sequence_and_contains_no_confidential_strings()
    {
        string[] forbidden =
        [
            SyntheticRun.AnswerCanary,
            SyntheticRun.SupportCanary,
            SyntheticRun.StudentCanary,
            SyntheticRun.QuestionCanary,
            SyntheticRun.PromptCanary,
            SyntheticRun.ReasonCanary,
            SyntheticRun.EvidenceCanary,
            SyntheticRun.CredentialCanary,
            "REALPATH-CANARY",
            "REALNAME-CANARY",
        ];

        // 1) 通常: ログが 1 件でき、ジョブ開始のローカル時刻の名前
        using SyntheticRun first = new();
        DateTime before = DateTime.Now;
        RunSummary summary = await first.RunAsync(cancellationToken: TestContext.Current.CancellationToken);
        DateTime after = DateTime.Now;
        Assert.True(File.Exists(Assert.IsType<string>(summary.FinalPath)));
        string[] logs = Directory.GetFiles(first.OutputDirectory, "*-log.txt");
        string logPath = Assert.Single(logs);
        string logName = Path.GetFileName(logPath);
        Assert.Matches(LogNamePattern(), logName);
        string[] allowedStamps =
        [
            before.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture),
            after.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture),
        ];
        Assert.Contains(logName[..12], allowedStamps);
        string content = await File.ReadAllTextAsync(logPath, Encoding.UTF8, TestContext.Current.CancellationToken);
        Assert.False(string.IsNullOrWhiteSpace(content), "経過ログが書かれている");
        foreach (string text in forbidden)
        {
            Assert.DoesNotContain(text, content, StringComparison.Ordinal);
        }

        Assert.DoesNotContain(first.Directory, content, StringComparison.OrdinalIgnoreCase);

        // 2) 同名が既にある: 上書きせず -02。既存 file の内容は変わらない（開始が分をまたぐ場合に備え次の分も用意）
        using SyntheticRun second = new();
        DateTime now = DateTime.Now;
        Dictionary<string, string> sentinels = [];
        foreach (DateTime stamp in new[] { now, now.AddMinutes(1) })
        {
            string existing = Path.Combine(
                second.OutputDirectory,
                stamp.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture) + "-log.txt");
            File.WriteAllText(existing, "EXISTING-LOG-SENTINEL");
            sentinels[existing] = "EXISTING-LOG-SENTINEL";
        }

        await second.RunAsync(cancellationToken: TestContext.Current.CancellationToken);
        foreach ((string path, string expected) in sentinels)
        {
            Assert.Equal(expected, File.ReadAllText(path));
        }

        string created = Assert.Single(
            Directory.GetFiles(second.OutputDirectory, "*-02-log.txt"));
        Assert.Matches(LogNameSecondPattern(), Path.GetFileName(created));
        string secondContent = File.ReadAllText(created, Encoding.UTF8);
        foreach (string text in forbidden)
        {
            Assert.DoesNotContain(text, secondContent, StringComparison.Ordinal);
        }

        // 3) ログを書けない（同名の directory が塞いでいる）場合も採点・自動出力は成功し、例外にならない
        using SyntheticRun blocked = new();
        DateTime start = DateTime.Now;
        foreach (DateTime stamp in new[] { start, start.AddMinutes(1) })
        {
            string stem = stamp.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture);
            Directory.CreateDirectory(Path.Combine(blocked.OutputDirectory, stem + "-log.txt"));
            Directory.CreateDirectory(Path.Combine(blocked.OutputDirectory, stem + "-02-log.txt"));
        }

        RunSummary blockedSummary = await blocked.RunAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(QuantificationRunStatusCodes.Success, blockedSummary.StatusCode);
        Assert.True(File.Exists(Assert.IsType<string>(blockedSummary.FinalPath)));
    }
}
