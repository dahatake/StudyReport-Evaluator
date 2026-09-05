using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using StudyReportEvaluator.App.Launch;
using StudyReportEvaluator.App.Navigation;
using StudyReportEvaluator.App.Tests.Workbooks.Mapping;
using StudyReportEvaluator.App.Tests.Workflow;
using StudyReportEvaluator.App.ViewModels;
using StudyReportEvaluator.App.Views;
using StudyReportEvaluator.App.Workflow;
using StudyReportEvaluator.App.Workbooks.Reading;
using StudyReportEvaluator.Core.Domain;
using Xunit;

namespace StudyReportEvaluator.App.Tests.UI;

public sealed class ResponsiveLayoutTests
{
    private const double ShellWidth = 1024d;
    private const double ShellHeight = 720d;
    private const double NarrowWidth = 760d;
    private const double NarrowHeight = 600d;

    // Absorbs layout rounding only; anything larger would hide a real overflow.
    private const double OverflowTolerance = 1d;

    private static readonly string LongJapaneseName =
        "設問1：機械学習とルールベースの違いを、具体例と評価指標を含めて詳細に説明してください（長文の表示名）";

    [AvaloniaFact]
    public async Task Shell_fits_within_the_minimum_window_width_across_every_step()
    {
        using X02TemporaryWorkbook workbook = X02SyntheticWorkbookFactory.CreateSingleSheet(
            "Original",
            1,
            12,
            2,
            new X02Header(1, "Answer"),
            new X02Header(2, "Rationale"));
        InputViewModel input = new();
        await input.SetFilePathAsync(workbook.Path, TestContext.Current.CancellationToken);
        QuantificationDefinition definition = input.DefinitionDraft;
        WorkbookMetadata metadata = input.Metadata
            ?? throw new InvalidOperationException("Workbook metadata is required.");
        RunSummary summary = await U04TestSupport.CreateSummaryAsync(definition, metadata);
        ExecutionViewModel execution = new(
            new RecordingAuthenticationBoundary(
                new ExecutionAuthenticationSnapshot(
                    ExecutionAuthenticationState.Available,
                    [U04TestSupport.Model("model-test")],
                    U04TestSupport.RuntimeIdentity())),
            new RecordingRunBoundary((_, progress, _) =>
            {
                progress?.Invoke(new EvaluationProgress(
                    summary.PlannedEvaluationCount,
                    summary.CompletedEvaluationCount,
                    0,
                    EvaluationProgressStatus.Completed));
                return Task.FromResult(summary);
            }));
        MainWindowViewModel viewModel = new(
            new WorkflowNavigator(),
            input,
            new QuantificationDesignViewModel(definition),
            execution,
            new ResultsOutputViewModel(new RecordingOutputBoundary()));
        MainWindow window = new(viewModel)
        {
            Width = ShellWidth,
            Height = ShellHeight,
        };

        try
        {
            window.Show();
            Render();

            ScrollViewer shell = Required<ScrollViewer>(window, "ShellScrollViewer");
            Assert.Equal(ScrollBarVisibility.Disabled, shell.HorizontalScrollBarVisibility);
            Assert.Equal(ScrollBarVisibility.Auto, shell.VerticalScrollBarVisibility);
            AssertFitsHorizontally(window, "input step");

            viewModel.NextCommand.Execute(null);
            Render();
            AssertFitsHorizontally(window, "design step");

            viewModel.NextCommand.Execute(null);
            Render();
            AssertFitsHorizontally(window, "execution step");

            await execution.CheckAuthenticationAsync(TestContext.Current.CancellationToken);
            await execution.StartAsync(TestContext.Current.CancellationToken);
            Render();

            Assert.Equal(WorkflowStep.Results, viewModel.CurrentStep);
            AssertFitsHorizontally(window, "results step");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Input_view_fits_and_scrolls_vertically_at_a_narrow_viewport()
    {
        using X02TemporaryWorkbook workbook = X02SyntheticWorkbookFactory.CreateSampleLike();
        InputViewModel input = new();
        await input.SetFilePathAsync(workbook.Path, TestContext.Current.CancellationToken);
        InputView view = new(input);
        Window window = Host(view);

        try
        {
            Assert.NotEmpty(input.Questions);
            AssertFitsHorizontally(view, "input view");
            AssertVerticalScrollContract(Required<ScrollViewer>(view, "InputScrollViewer"));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Design_view_fits_and_scrolls_vertically_at_a_narrow_viewport()
    {
        QuantificationDesignViewModel design = new(ManyItemDefinition());
        design.SelectedQuestion = design.Questions[0];
        design.Questions[0].SelectedEvaluator = design.Questions[0].Evaluators[0];
        QuantificationDesignView view = new(design);
        Window window = Host(view);

        try
        {
            Assert.Equal(3, design.Questions.Count);
            AssertFitsHorizontally(view, "design view");
            AssertVerticalScrollContract(
                Required<ScrollViewer>(view, "QuantificationDesignScrollViewer"));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Execution_view_fits_and_scrolls_vertically_at_a_narrow_viewport()
    {
        QuantificationDefinition definition = U04TestSupport.Definition(2, 12);
        WorkbookMetadata metadata = U01TestSupport.ValidateMapping(definition).Metadata;
        RunSummary summary = await U04TestSupport.CreateSummaryAsync(definition, metadata);
        ExecutionViewModel execution = U04TestSupport.ConfiguredExecutionViewModel(
            definition,
            metadata,
            new RecordingRunBoundary((_, _, _) => Task.FromResult(summary)));
        await execution.CheckAuthenticationAsync(TestContext.Current.CancellationToken);
        ExecutionView view = new(execution);
        Window window = Host(view);

        try
        {
            AssertFitsHorizontally(view, "execution view");
            AssertVerticalScrollContract(Required<ScrollViewer>(view, "ExecutionScrollViewer"));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Results_view_fits_and_scrolls_vertically_at_a_narrow_viewport()
    {
        QuantificationDefinition definition = U04TestSupport.Definition(2, 60);
        WorkbookMetadata metadata = U01TestSupport.ValidateMapping(definition).Metadata;
        RunSummary summary = await U04TestSupport.CreateSummaryAsync(definition, metadata);
        ResultsOutputViewModel results = new(
            new RecordingOutputBoundary(),
            U04TestSupport.Context(summary));
        ResultsOutputView view = new(results);
        Window window = Host(view);

        try
        {
            Assert.Equal(59, results.Results.Count);
            AssertFitsHorizontally(view, "results view");
            ScrollViewer outerScroll = Required<ScrollViewer>(view, "ResultsOutputScrollViewer");

            ListBox reviewList = Required<ListBox>(view, "ResultsList");
            ListBox rowScoreList = Required<ListBox>(view, "RowScoreList");
            Assert.Equal(
                ScrollBarVisibility.Disabled,
                ScrollViewer.GetHorizontalScrollBarVisibility(reviewList));
            Assert.Equal(
                ScrollBarVisibility.Auto,
                ScrollViewer.GetVerticalScrollBarVisibility(reviewList));
            ScrollViewer reviewScroll = Assert.Single(
                reviewList.GetVisualDescendants().OfType<ScrollViewer>(),
                scroll => ReferenceEquals(scroll.TemplatedParent, reviewList));
            ScrollViewer rowScoreScroll = Assert.Single(
                rowScoreList.GetVisualDescendants().OfType<ScrollViewer>(),
                scroll => ReferenceEquals(scroll.TemplatedParent, rowScoreList));
            AssertVerticalScrollContract(reviewScroll);
            AssertVerticalScrollContract(rowScoreScroll);
            AssertVerticalScrollContract(outerScroll);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Long_japanese_names_and_paths_do_not_widen_the_layout()
    {
        QuantificationDesignViewModel design = new(ManyItemDefinition());
        design.Questions[0].DisplayName = LongJapaneseName;
        design.SelectedQuestion = design.Questions[0];
        QuantificationDesignView designView = new(design);
        Window designWindow = Host(designView);

        try
        {
            AssertFitsHorizontally(designView, "design view with a long question name");
            TextBlock[] longNames = designView.GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(text => text.Text == LongJapaneseName)
                .ToArray();
            Assert.NotEmpty(longNames);
            Assert.All(longNames, longName =>
            {
                AssertControlFitsHorizontally(longName, designView, "long design question name");
            });
        }
        finally
        {
            designWindow.Close();
        }

        QuantificationDefinition definition = U04TestSupport.Definition(2, 6);
        WorkbookMetadata metadata = U01TestSupport.ValidateMapping(definition).Metadata;
        RunSummary summary = await U04TestSupport.CreateSummaryAsync(definition, metadata);
        ResultsOutputViewModel results = new(
            new RecordingOutputBoundary(),
            U04TestSupport.Context(summary))
        {
            OutputPath = @"C:\とても\長い\日本語の\出力先\ディレクトリ\階層\eval-20260905-1200-reviewed-with-a-very-long-name.xlsx",
        };
        results.Results[0].OverrideText = "この値は範囲外です";
        ResultsOutputView resultsView = new(results);
        Window resultsWindow = Host(resultsView);

        try
        {
            Assert.True(results.HasOverrideErrors);
            AssertFitsHorizontally(resultsView, "results view with a long output path");
            TextBox outputPath = Required<TextBox>(resultsView, "OutputPathTextBox");
            Assert.Equal(Avalonia.Media.TextWrapping.Wrap, outputPath.TextWrapping);
            AssertControlFitsHorizontally(outputPath, resultsView, "long results output path");
            ResultsCriterionViewModel firstResult = results.Results[0];
            Border resultCard = Assert.Single(
                resultsView.GetVisualDescendants().OfType<Border>(),
                border => AutomationProperties.GetAutomationId(border) == firstResult.AutomationId);
            AssertControlFitsHorizontally(resultCard, resultsView, "result review card");
        }
        finally
        {
            resultsWindow.Close();
        }
    }

    [AvaloniaFact]
    public async Task Results_rows_stay_virtualized_after_the_responsive_reflow()
    {
        QuantificationDefinition definition = U04TestSupport.Definition(2, 531);
        WorkbookMetadata metadata = U01TestSupport.ValidateMapping(definition).Metadata;
        RunSummary summary = await U04TestSupport.CreateSummaryAsync(definition, metadata);
        ResultsOutputViewModel results = new(
            new RecordingOutputBoundary(),
            U04TestSupport.Context(summary));
        ResultsOutputView view = new(results);
        Window window = Host(view);

        try
        {
            Assert.Equal(530, results.Results.Count);
            Assert.Contains(view.GetVisualDescendants(), visual => visual is VirtualizingStackPanel);
            int realizedOverrideEditors = view.GetVisualDescendants()
                .OfType<TextBox>()
                .Count(textBox => (AutomationProperties.GetAutomationId(textBox) ?? string.Empty)
                    .EndsWith("-Override", StringComparison.Ordinal));
            Assert.InRange(
                realizedOverrideEditors,
                1,
                30);
            AssertFitsHorizontally(view, "results view with 530 rows");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Startup_error_window_scrolls_vertically_and_fits_its_minimum_width()
    {
        StartupErrorWindow window = new(
            new LaunchOptionsException(LaunchErrorCodes.PromptLengthInvalid, "--prompt"))
        {
            Width = 420,
            Height = 220,
        };

        try
        {
            window.Show();
            Render();

            ScrollViewer scroll = Assert.Single(
                window.GetVisualDescendants().OfType<ScrollViewer>(),
                item => AutomationProperties.GetAutomationId(item) == "StartupErrorScrollViewer");
            Button close = Assert.Single(
                window.GetVisualDescendants().OfType<Button>(),
                item => AutomationProperties.GetAutomationId(item) == "CloseStartupError");
            Assert.Equal(ScrollBarVisibility.Disabled, scroll.HorizontalScrollBarVisibility);
            Assert.Equal(ScrollBarVisibility.Auto, scroll.VerticalScrollBarVisibility);
            Assert.True(close.IsEffectivelyVisible);
            AssertFitsHorizontally(window, "startup error window");

            StackPanel content = Assert.IsType<StackPanel>(scroll.Content);
            for (int index = 0; index < 8; index++)
            {
                content.Children.Insert(
                    content.Children.Count - 1,
                    new TextBlock
                    {
                        Text = $"追加の起動案内 {index + 1}",
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    });
            }

            Render();
            AssertVerticalScrollContract(scroll);
            Point closeOrigin = close.TranslatePoint(default, scroll)
                ?? throw new InvalidOperationException("Unable to locate the close button in the startup error scroll viewer.");
            Assert.True(
                closeOrigin.Y >= -OverflowTolerance
                    && closeOrigin.Y + close.Bounds.Height <= scroll.Viewport.Height + OverflowTolerance,
                "The close button must be reachable at the vertical scroll endpoint.");
        }
        finally
        {
            window.Close();
        }
    }

    private static QuantificationDefinition ManyItemDefinition() =>
        U01TestSupport.Definition(
            2,
            40,
            U01TestSupport.Question("Q1", "A", ["B"], true, U01TestSupport.Evaluator("E1", "C1")),
            U01TestSupport.Question("Q2", "C", [], true, U01TestSupport.Evaluator("E2", "C2")),
            U01TestSupport.Question("Q3", "A", ["B"], true, U01TestSupport.Evaluator("E3", "C3")));

    private static Window Host(Control view)
    {
        Window window = new()
        {
            Width = NarrowWidth,
            Height = NarrowHeight,
            Content = view,
        };
        window.Show();
        Render();
        return window;
    }

    private static void AssertVerticalScrollContract(ScrollViewer scrollViewer)
    {
        Assert.Equal(ScrollBarVisibility.Disabled, scrollViewer.HorizontalScrollBarVisibility);
        Assert.Equal(ScrollBarVisibility.Auto, scrollViewer.VerticalScrollBarVisibility);
        Assert.True(
            scrollViewer.Extent.Width <= scrollViewer.Viewport.Width + OverflowTolerance,
            $"Content extent {scrollViewer.Extent.Width:F1} exceeds viewport {scrollViewer.Viewport.Width:F1}.");
        Assert.True(
            scrollViewer.Extent.Height > scrollViewer.Viewport.Height,
            "The fixture does not produce enough content to require vertical scrolling.");
        Assert.Contains(
            scrollViewer.GetVisualDescendants().OfType<ScrollBar>(),
            scrollBar => scrollBar.Orientation == Orientation.Vertical
                && scrollBar.IsEffectivelyVisible);

        // Virtualized descendants can increase Extent while containers are realized.
        // Retry until both Extent and the terminal offset stabilize, with a finite failure bound.
        double previousExtent = -1d;
        for (int attempt = 0; attempt < 8; attempt++)
        {
            scrollViewer.ScrollToEnd();
            Render();
            double currentBottom = scrollViewer.Extent.Height - scrollViewer.Viewport.Height;
            if (Math.Abs(scrollViewer.Extent.Height - previousExtent) <= OverflowTolerance
                && Math.Abs(scrollViewer.Offset.Y - currentBottom) <= OverflowTolerance)
            {
                break;
            }

            previousExtent = scrollViewer.Extent.Height;
        }

        double expectedBottom = scrollViewer.Extent.Height - scrollViewer.Viewport.Height;
        Assert.Equal(expectedBottom, scrollViewer.Offset.Y, OverflowTolerance);
    }

    private static void AssertControlFitsHorizontally(
        Control control,
        Control root,
        string context)
    {
        Point origin = control.TranslatePoint(default, root)
            ?? throw new InvalidOperationException($"Unable to locate {context} within its root.");
        Assert.True(
            origin.X >= -OverflowTolerance
                && origin.X + control.Bounds.Width <= root.Bounds.Width + OverflowTolerance,
            $"The {context} extends from {origin.X:F1} to {origin.X + control.Bounds.Width:F1} "
                + $"within a {root.Bounds.Width:F1}px root.");
    }

    private static void AssertFitsHorizontally(Control root, string context)
    {
        double available = root is TopLevel topLevel
            ? topLevel.ClientSize.Width
            : root.Bounds.Width;
        Assert.True(available > 0d, $"The layout pass produced no width for the {context}.");

        List<string> overflowing = [];
        foreach (Visual visual in root.GetVisualDescendants())
        {
            // Template internals such as text presenters are clipped by their owning control,
            // and Viewbox children are scaled to fit, so neither can overflow the window.
            if (visual is not Control control
                || control.TemplatedParent is not null
                || control.FindAncestorOfType<Viewbox>() is not null
                || !control.IsEffectivelyVisible
                || control.Bounds.Width <= 0d)
            {
                continue;
            }

            if (control.TranslatePoint(default, root) is not Point origin)
            {
                continue;
            }

            double left = origin.X;
            double right = left + control.Bounds.Width;
            if (left < -OverflowTolerance || right > available + OverflowTolerance)
            {
                overflowing.Add($"{Describe(control)} left={left:F1} right={right:F1}");
            }
        }

        Assert.True(
            overflowing.Count == 0,
            $"Controls overflow the {available:F0}px wide viewport in the {context}: "
                + string.Join("; ", overflowing.Take(8)));
    }

    private static string Describe(Control control) =>
        string.IsNullOrWhiteSpace(control.Name)
            ? control.GetType().Name
            : $"{control.GetType().Name}#{control.Name}";

    private static T Required<T>(Control root, string name)
        where T : Control =>
        Assert.IsType<T>(root.FindControl<T>(name));

    private static void Render()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }
}
