using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using StudyReportEvaluator.App.ViewModels;
using StudyReportEvaluator.App.Views;
using Xunit;
using Control = Avalonia.Controls.Control;

namespace StudyReportEvaluator.App.Tests.UI;

/// <summary>FR-117: row-status counts on the results screen.</summary>
public sealed class ResultsRowStatusCountsTests
{
    // FR-117 AC-091
    [Fact]
    public void Ac091_mixed_rows_are_counted_in_list_status_names_and_sum_to_all_rows()
    {
        List<ResultsRowStatus> rows = [];
        rows.AddRange(Enumerable.Repeat(ResultsRowStatus.Success, 6));
        rows.AddRange(Enumerable.Repeat(ResultsRowStatus.TechnicalError, 2));
        rows.Add(ResultsRowStatus.Empty);
        rows.Add(ResultsRowStatus.Cancelled);
        Assert.Equal(10, rows.Count);
        Assert.Equal("成功 6 · 技術エラー 2 · 回答空欄 1 · 取消 1 · 未処理・未確定 0",
            ResultsOutputViewModel.DescribeRowStatusCounts(rows));
    }

    // FR-117 AC-091
    [Fact]
    public void Ac091_zero_technical_errors_shows_zero_and_unloaded_is_empty()
    {
        Assert.Equal("成功 3 · 技術エラー 0 · 回答空欄 0 · 取消 0 · 未処理・未確定 0",
            ResultsOutputViewModel.DescribeRowStatusCounts(Enumerable.Repeat(ResultsRowStatus.Success, 3)));
        using ResultsOutputViewModel viewModel = new(new RecordingOutputBoundary());
        Assert.Equal(string.Empty, viewModel.RowStatusCountsText);
    }

    // FR-117 AC-092
    [AvaloniaFact]
    public async Task Ac092_counts_ignore_page_and_selection_update_on_reload_and_are_not_trimmed()
    {
        ExecutionRunContext many = await new ResultsDurableFixture().RunAsync(rowCount: 60);
        ExecutionRunContext cancelled = await new ResultsDurableFixture().RunAsync(rowCount: 2, cancelAfterRows: 1);
        using ResultsOutputViewModel viewModel = new(new RecordingOutputBoundary());
        View host = new(viewModel);
        TextBlock counts = Assert.Single(host.Root.GetVisualDescendants().OfType<TextBlock>(),
            c => AutomationProperties.GetAutomationId(c) == "ResultsRowStatusCounts");
        Assert.False(counts.IsEffectivelyVisible);

        viewModel.Load(many);
        Render();
        const string all = "成功 60 · 技術エラー 0 · 回答空欄 0 · 取消 0 · 未処理・未確定 0";
        Assert.Equal(all, counts.Text);
        Assert.Equal(all, AutomationProperties.GetName(counts));
        Assert.True(counts.IsEffectivelyVisible);
        Assert.NotNull(counts.TextLayout);
        Assert.DoesNotContain(counts.TextLayout.TextLines, line => line.HasCollapsed);

        Assert.True(viewModel.RowScores.Count > viewModel.PageSize);
        viewModel.NextPageCommand.Execute(null);
        viewModel.SelectedRow = viewModel.VisibleRowScores[0];
        Render();
        Assert.Equal(all, counts.Text);

        viewModel.Load(cancelled);
        Render();
        Assert.Equal("成功 1 · 技術エラー 0 · 回答空欄 0 · 取消 0 · 未処理・未確定 1", counts.Text);
        host.Window.Close();
    }

    private static void Render()
    {
        for (int pass = 0; pass < 3; pass++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

    private sealed class View
    {
        public View(ResultsOutputViewModel viewModel)
        {
            Root = new ResultsOutputView(viewModel);
            Window = new Window { Width = 950, Height = 450, Content = Root };
            Window.Show();
            Render();
        }

        public Control Root { get; }

        public Window Window { get; }
    }
}

