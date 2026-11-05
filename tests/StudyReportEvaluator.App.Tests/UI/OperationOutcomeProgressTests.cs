using StudyReportEvaluator.App.Copilot;
using StudyReportEvaluator.App.Tests.Workflow;
using StudyReportEvaluator.App.ViewModels;
using StudyReportEvaluator.App.Workbooks.Reading;
using StudyReportEvaluator.App.Workbooks.Writing;
using StudyReportEvaluator.App.Workflow;
using StudyReportEvaluator.Core.Domain;
using Xunit;

namespace StudyReportEvaluator.App.Tests.UI;

/// <summary>FR-119: 「3 実行」の進捗文に成功数・失敗数を示す。</summary>
public sealed class OperationOutcomeProgressTests
{
    // FR-119 AC-097
    [Fact]
    public async Task FR119_AC097_running_progress_text_shows_success_and_failure_in_fixed_order()
    {
        (QuantificationDefinition definition, WorkbookMetadata metadata) = Design();
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource reported = new(TaskCreationOptions.RunContinuationsAsynchronously);
        RunSummary summary = await U04TestSupport.CreateSummaryAsync(definition, metadata);
        RecordingRunBoundary runner = new(async (_, progress, _) =>
        {
            progress?.Invoke(new EvaluationProgress(10, 0, 0, EvaluationProgressStatus.Running));
            progress?.Invoke(new EvaluationProgress(
                10, 6, 2, EvaluationProgressStatus.Running, succeeded: 4, failed: 1));
            reported.SetResult();
            await release.Task;
            return summary;
        });
        using ExecutionViewModel viewModel = U04TestSupport.ConfiguredExecutionViewModel(definition, metadata, runner);
        Assert.Equal("まだ実行していません。", viewModel.ProgressText);
        await viewModel.CheckAuthenticationAsync(TestContext.Current.CancellationToken);

        Task run = viewModel.StartAsync(TestContext.Current.CancellationToken);
        await reported.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Equal("6 / 10 完了 · 成功 4 · 失敗 1 · 2 実行中 · 60.0%", viewModel.ProgressText);
        Assert.Equal("処理単位: 6 / 10 完了 · 成功 4 · 失敗 1 · 2 実行中 · 60.0%", "処理単位: " + viewModel.ProgressText);

        release.SetResult();
        await run.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    // FR-119 AC-097: 件数未報告の間は成功・失敗を表示しない（0 と偽らない）
    [Fact]
    public async Task FR119_AC097_unreported_counts_do_not_show_success_or_failure()
    {
        (QuantificationDefinition definition, WorkbookMetadata metadata) = Design();
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource reported = new(TaskCreationOptions.RunContinuationsAsynchronously);
        RunSummary summary = await U04TestSupport.CreateSummaryAsync(definition, metadata);
        RecordingRunBoundary runner = new(async (_, progress, _) =>
        {
            progress?.Invoke(new EvaluationProgress(10, 3, 1, EvaluationProgressStatus.Running));
            reported.SetResult();
            await release.Task;
            return summary;
        });
        using ExecutionViewModel viewModel = U04TestSupport.ConfiguredExecutionViewModel(definition, metadata, runner);
        await viewModel.CheckAuthenticationAsync(TestContext.Current.CancellationToken);

        Task run = viewModel.StartAsync(TestContext.Current.CancellationToken);
        await reported.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Equal("3 / 10 完了 · 1 実行中 · 30.0%", viewModel.ProgressText);
        Assert.DoesNotContain("成功", viewModel.ProgressText, StringComparison.Ordinal);
        Assert.DoesNotContain("失敗", viewModel.ProgressText, StringComparison.Ordinal);

        release.SetResult();
        await run.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    // FR-119 AC-097: 状態コードの分類（成功＝Success・Empty、失敗＝それら・NotRunZeroBudget 以外）
    [Theory]
    [InlineData(ResultsStatusCodes.Success, true, false)]
    [InlineData(ResultsStatusCodes.Empty, true, false)]
    [InlineData(ResultsStatusCodes.NotRunZeroBudget, false, false)]
    [InlineData("TECHNICAL_ERROR", false, true)]
    [InlineData(ResultsStatusCodes.Cancelled, false, false)]
    public void FR119_AC097_status_classification_matches_operation_failure_definition(
        string status, bool success, bool failure)
    {
        Assert.Equal(success, OperationOutcomeCounter.IsSuccess(status));
        Assert.Equal(failure, OperationOutcomeCounter.IsFailure(status));
    }

    // FR-119 AC-097: Success 3・Empty 1・技術エラー 1・Cancelled 2 → 成功 4・失敗 1（取消はどちらにも数えない）
    [Fact]
    public void FR119_AC097_cancelled_is_counted_as_neither_success_nor_failure()
    {
        string[] statuses =
        [
            ResultsStatusCodes.Success, ResultsStatusCodes.Success, ResultsStatusCodes.Success,
            ResultsStatusCodes.Empty, "TECHNICAL_ERROR",
            ResultsStatusCodes.Cancelled, ResultsStatusCodes.Cancelled,
        ];

        Assert.Equal(4, statuses.Count(OperationOutcomeCounter.IsSuccess));
        Assert.Equal(1, statuses.Count(OperationOutcomeCounter.IsFailure));
    }

    // FR-119 AC-098
    [Fact]
    public async Task FR119_AC098_completed_run_uses_summary_failure_count_and_derived_success_count()
    {
        (QuantificationDefinition definition, WorkbookMetadata metadata) = Design();
        RunSummary summary = await U04TestSupport.CreateSummaryAsync(definition, metadata);
        RecordingRunBoundary runner = new((_, progress, _) =>
        {
            progress?.Invoke(new EvaluationProgress(
                summary.PlannedOperationCount, 1, 1, EvaluationProgressStatus.Running, succeeded: 0, failed: 0));
            return Task.FromResult(summary);
        });
        using ExecutionViewModel viewModel = U04TestSupport.ConfiguredExecutionViewModel(definition, metadata, runner);
        await viewModel.CheckAuthenticationAsync(TestContext.Current.CancellationToken);

        await viewModel.StartAsync(TestContext.Current.CancellationToken);

        int failed = summary.OperationFailureCount;
        int succeeded = summary.OperationSuccessCount;
        Assert.Equal(summary.CompletedOperationCount, succeeded + failed);
        Assert.Contains(
            $" · 成功 {succeeded:N0} · 失敗 {failed:N0} · 0 実行中 · ",
            viewModel.ProgressText,
            StringComparison.Ordinal);
    }

    // FR-119 AC-099
    [Fact]
    public void FR119_AC099_old_wording_is_gone_and_progress_labels_describe_success_and_failure()
    {
        string views = Path.Combine(AppDir(), "Views");
        string execution = File.ReadAllText(Path.Combine(views, "ExecutionView.axaml"));
        string main = File.ReadAllText(Path.Combine(views, "MainWindow.axaml"));

        foreach (string text in new[] { execution, main })
        {
            Assert.DoesNotContain("成功数とは別", text, StringComparison.Ordinal);
            Assert.DoesNotContain("送信回数とは別", text, StringComparison.Ordinal);
        }

        Assert.Contains("処理単位の完了数・成功数・失敗数・実行中の件数です。学生行数やAIへの送信回数ではありません", execution, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"{Binding ProgressText, StringFormat='処理単位: {0}'}\"", execution, StringComparison.Ordinal);
        Assert.Contains("今回runの処理単位の完了数・成功数・失敗数・実行中の件数", main, StringComparison.Ordinal);
    }

    private static (QuantificationDefinition Definition, WorkbookMetadata Metadata) Design()
    {
        QuantificationDefinition definition = U04TestSupport.Definition(2, 3);
        return (definition, U01TestSupport.ValidateMapping(definition).Metadata);
    }

    private static string AppDir()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "src", "StudyReportEvaluator.App")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        return Path.Combine(dir ?? throw new InvalidOperationException("repo root not found"), "src", "StudyReportEvaluator.App");
    }
}
