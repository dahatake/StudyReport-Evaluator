using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using StudyReportEvaluator.App.Composition;
using StudyReportEvaluator.App.Navigation;
using StudyReportEvaluator.App.Tests.Workbooks.Mapping;
using StudyReportEvaluator.App.ViewModels;
using StudyReportEvaluator.App.Views;
using StudyReportEvaluator.App.Workflow;
using StudyReportEvaluator.Core.Domain;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(StudyReportEvaluator.App.Tests.UI.U02TestAppBuilder))]

namespace StudyReportEvaluator.App.Tests.UI;

public sealed class U02TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = false,
            });
}

public sealed class MainWindowTests
{
    [Fact]
    public void Runtime_XAML_loader_has_a_public_parameterless_constructor()
    {
        Assert.NotNull(typeof(MainWindow).GetConstructor(Type.EmptyTypes));
    }

    [Fact]
    public void Navigator_has_exactly_four_closed_ordered_steps_and_sequential_forward_navigation()
    {
        WorkflowNavigator navigator = new();

        Assert.Equal(4, WorkflowNavigator.StepCount);
        Assert.Equal(
            [WorkflowStep.Input, WorkflowStep.Design, WorkflowStep.Execution, WorkflowStep.Results],
            Enum.GetValues<WorkflowStep>());
        Assert.Equal(
            [WorkflowStep.Input, WorkflowStep.Design, WorkflowStep.Execution, WorkflowStep.Results],
            navigator.Steps.Select(step => step.Step));
        Assert.Equal([1, 2, 3, 4], navigator.Steps.Select(step => step.Number));
        Assert.Equal(WorkflowStep.Input, navigator.CurrentStep);
        Assert.False(navigator.CanNavigateTo(WorkflowStep.Execution));
        Assert.False(navigator.NavigateTo(WorkflowStep.Results));

        Assert.True(navigator.MoveNext());
        Assert.Equal(WorkflowStep.Design, navigator.CurrentStep);
        Assert.Equal(WorkflowStepState.Completed, navigator.GetState(WorkflowStep.Input));
        Assert.Equal(WorkflowStepState.Current, navigator.GetState(WorkflowStep.Design));
        Assert.Equal(WorkflowStepState.Upcoming, navigator.GetState(WorkflowStep.Execution));
        Assert.True(navigator.MoveNext());
        Assert.True(navigator.MoveNext());
        Assert.Equal(WorkflowStep.Results, navigator.CurrentStep);
        Assert.False(navigator.MoveNext());
        Assert.True(navigator.MovePrevious());
        Assert.Equal(WorkflowStep.Execution, navigator.CurrentStep);
        Assert.True(navigator.NavigateTo(WorkflowStep.Input));
        Assert.True(navigator.CanNavigateTo(WorkflowStep.Results));
        Assert.Throws<ArgumentOutOfRangeException>(() => navigator.NavigateTo((WorkflowStep)99));
    }

    [AvaloniaFact]
    public void App_factory_creates_constructor_injected_shell_with_input_as_the_initial_step()
    {
        App app = Assert.IsType<App>(Application.Current);
        MainWindow window = app.CreateMainWindow();

        try
        {
            Assert.Same(window.ViewModel, window.DataContext);
            Assert.Same(app.Services.WorkflowNavigator, window.ViewModel.Navigator);
            Assert.Equal(WorkflowStep.Input, window.ViewModel.CurrentStep);
            Assert.Equal("StudyReport Evaluator", window.Title);
            Assert.Equal(1024d, window.MinWidth);
            Assert.Equal(720d, window.MinHeight);

            window.Show();
            Dispatcher.UIThread.RunJobs();

            Border warning = Required<Border>(window, "EthicsWarningBanner");
            Button input = Required<Button>(window, "InputStepButton");
            ContentControl content = Required<ContentControl>(window, "CurrentStepContent");
            Assert.True(warning.IsVisible);
            Assert.Same(input, window.FocusManager?.GetFocusedElement());
            Assert.Same(window.ViewModel.InputViewModel, content.Content);
            InputView inputView = Assert.Single(window.GetVisualDescendants().OfType<InputView>());
            Assert.Same(window.ViewModel.InputViewModel, inputView.DataContext);
            Assert.False(Required<Grid>(window, "CurrentStepPlaceholderHost").IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_navigation_exposes_automation_ids_and_reaches_all_four_states()
    {
        MainWindow window = new ServiceRegistration().CreateMainWindow();

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Button input = Required<Button>(window, "InputStepButton");
            Button design = Required<Button>(window, "DesignStepButton");
            Button execution = Required<Button>(window, "ExecutionStepButton");
            Button results = Required<Button>(window, "ResultsStepButton");
            Button next = Required<Button>(window, "NextStepButton");

            Assert.Equal("WorkflowStepInput", AutomationProperties.GetAutomationId(input));
            Assert.Equal("WorkflowStepDesign", AutomationProperties.GetAutomationId(design));
            Assert.Equal("WorkflowStepExecution", AutomationProperties.GetAutomationId(execution));
            Assert.Equal("WorkflowStepResults", AutomationProperties.GetAutomationId(results));
            Assert.Equal([0, 1, 2, 3], new[] { input, design, execution, results }.Select(button => button.TabIndex));
            Assert.True(input.IsEffectivelyEnabled);
            Assert.True(design.IsEffectivelyEnabled);
            Assert.False(execution.IsEffectivelyEnabled);
            Assert.False(results.IsEffectivelyEnabled);
            Assert.Contains("現在", window.ViewModel.InputStep.StatusText, StringComparison.Ordinal);
            Assert.Equal("●", window.ViewModel.InputStep.StatusIcon);
            Assert.Contains("current", input.Classes);
            Assert.Contains("upcoming", execution.Classes);
            Assert.Single(window.GetVisualDescendants().OfType<InputView>());

            Assert.True(input.Focus(NavigationMethod.Tab, KeyModifiers.None));
            Press(window, Key.Tab);
            Assert.True(design.IsFocused);
            Press(window, Key.Enter);
            Assert.Equal(WorkflowStep.Design, window.ViewModel.CurrentStep);
            Assert.Equal("完了", window.ViewModel.InputStep.StatusText);
            Assert.Equal("✓", window.ViewModel.InputStep.StatusIcon);
            Assert.Contains("現在", window.ViewModel.DesignStep.StatusText, StringComparison.Ordinal);
            Assert.Contains("completed", input.Classes);
            Assert.Contains("current", design.Classes);
            QuantificationDesignView designView = Assert.Single(
                window.GetVisualDescendants().OfType<QuantificationDesignView>());
            Assert.Same(window.ViewModel.DesignViewModel, designView.DataContext);

            Assert.True(next.Focus(NavigationMethod.Tab, KeyModifiers.None));
            Press(window, Key.Enter);
            Assert.Equal(WorkflowStep.Execution, window.ViewModel.CurrentStep);
            ExecutionView executionView = Assert.Single(
                window.GetVisualDescendants().OfType<ExecutionView>());
            Assert.Same(window.ViewModel.ExecutionViewModel, executionView.DataContext);
            Press(window, Key.Enter);
            Assert.Equal(WorkflowStep.Results, window.ViewModel.CurrentStep);
            ResultsOutputView resultsView = Assert.Single(
                window.GetVisualDescendants().OfType<ResultsOutputView>());
            Assert.Same(window.ViewModel.ResultsOutputViewModel, resultsView.DataContext);
            Assert.Equal("完了", window.ViewModel.ExecutionStep.StatusText);
            Assert.Contains("現在", window.ViewModel.ResultsStep.StatusText, StringComparison.Ordinal);
            Assert.False(next.IsEffectivelyEnabled);
            Assert.True(Required<Border>(window, "EthicsWarningBanner").IsVisible);
            Assert.False(Required<Grid>(window, "CurrentStepPlaceholderHost").IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Shell_is_scrollable_focus_visible_and_usable_at_two_hundred_percent_scale()
    {
        MainWindow window = new ServiceRegistration().CreateMainWindow();

        try
        {
            window.Show();
            window.SetRenderScaling(2d);
            Dispatcher.UIThread.RunJobs();

            ScrollViewer scrollViewer = Required<ScrollViewer>(window, "ShellScrollViewer");
            Button input = Required<Button>(window, "InputStepButton");
            Button next = Required<Button>(window, "NextStepButton");

            Assert.Equal(2d, window.RenderScaling);
            Assert.Equal(ScrollBarVisibility.Auto, scrollViewer.HorizontalScrollBarVisibility);
            Assert.Equal(ScrollBarVisibility.Auto, scrollViewer.VerticalScrollBarVisibility);
            Assert.True(input.MinHeight >= 44d);
            Assert.True(input.MinWidth >= 44d);
            Assert.True(next.MinHeight >= 44d);
            Assert.True(input.Focus(NavigationMethod.Tab, KeyModifiers.None));
            Assert.Same(input, window.FocusManager?.GetFocusedElement());
            Assert.Equal(new Thickness(3d), input.BorderThickness);
            Assert.True(next.Focus(NavigationMethod.Tab, KeyModifiers.None));
            Assert.Same(next, window.FocusManager?.GetFocusedElement());
            Assert.Equal(new Thickness(3d), next.BorderThickness);
            Assert.True(Required<Border>(window, "EthicsWarningBanner").IsVisible);
            Assert.True(Required<ContentControl>(window, "CurrentStepContent").IsVisible);
            Assert.False(Required<Grid>(window, "CurrentStepPlaceholderHost").IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Shell_round_trips_loaded_input_mapping_and_quantification_design_without_losing_edits()
    {
        using X02TemporaryWorkbook workbook = X02SyntheticWorkbookFactory.CreateSampleLike();
        WorkflowNavigator navigator = new();
        InputViewModel inputViewModel = new();
        await inputViewModel.SetFilePathAsync(workbook.Path, TestContext.Current.CancellationToken);
        MainWindowViewModel viewModel = new(
            navigator,
            inputViewModel,
            new QuantificationDesignViewModel());
        MainWindow window = new(viewModel);

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            Assert.Single(window.GetVisualDescendants().OfType<InputView>());

            viewModel.NextCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            QuantificationDesignView designView = Assert.Single(
                window.GetVisualDescendants().OfType<QuantificationDesignView>());
            Assert.Same(viewModel.DesignViewModel, designView.DataContext);
            Assert.Equal("Original", viewModel.DesignViewModel.Draft.SourceSheet);
            Assert.Equal(
                inputViewModel.DefinitionDraft.Questions.Select(question => question.PrimarySourceColumn),
                viewModel.DesignViewModel.Draft.Questions.Select(question => question.PrimarySourceColumn));

            viewModel.DesignViewModel.DefinitionName = "往復後も保持する設計";
            viewModel.PreviousCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("往復後も保持する設計", inputViewModel.DefinitionDraft.Name);

            inputViewModel.FirstDataRow = 3;
            viewModel.NextCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("往復後も保持する設計", viewModel.DesignViewModel.DefinitionName);
            Assert.Equal(3, viewModel.DesignViewModel.Draft.FirstDataRow);
            Assert.Single(window.GetVisualDescendants().OfType<QuantificationDesignView>());
            Assert.False(Required<Grid>(window, "CurrentStepPlaceholderHost").IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Shell_configures_execution_and_hands_the_completed_run_context_to_results()
    {
        using X02TemporaryWorkbook workbook = X02SyntheticWorkbookFactory.CreateSingleSheet(
            "Original",
            1,
            2,
            2,
            new X02Header(1, "Answer"),
            new X02Header(2, "Rationale"));
        InputViewModel input = new();
        await input.SetFilePathAsync(workbook.Path, TestContext.Current.CancellationToken);
        QuantificationDefinition definition = input.DefinitionDraft;
        RunSummary summary = await U04TestSupport.CreateSummaryAsync(
            definition,
            input.Metadata ?? throw new InvalidOperationException("Workbook metadata is required."));
        RecordingRunBoundary runner = new((_, progress, _) =>
        {
            progress?.Invoke(new EvaluationProgress(1, 1, 0, EvaluationProgressStatus.Completed));
            return Task.FromResult(summary);
        });
        ExecutionViewModel execution = new(
            new RecordingAuthenticationBoundary(
                new ExecutionAuthenticationSnapshot(
                    ExecutionAuthenticationState.Available,
                    [U04TestSupport.Model("model-test")],
                    U04TestSupport.RuntimeIdentity())),
            runner);
        RecordingOutputBoundary output = new();
        ResultsOutputViewModel results = new(output);
        MainWindowViewModel viewModel = new(
            new WorkflowNavigator(),
            input,
            new QuantificationDesignViewModel(definition),
            execution,
            results);
        MainWindow window = new(viewModel);

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            viewModel.NextCommand.Execute(null);
            viewModel.NextCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(WorkflowStep.Execution, viewModel.CurrentStep);
            Assert.True(execution.IsConfigured);
            Assert.Single(window.GetVisualDescendants().OfType<ExecutionView>());
            await execution.CheckAuthenticationAsync(TestContext.Current.CancellationToken);
            await execution.StartAsync(TestContext.Current.CancellationToken);

            QuantificationRunRequest request = Assert.IsType<QuantificationRunRequest>(runner.LastRequest);
            Assert.Equal(workbook.Path, request.InputPath);
            Assert.Equal(definition.Name, request.DraftDefinition.Name);
            Assert.True(results.IsLoaded);
            Assert.Equal(summary.CompletedEvaluationCount, results.CompletedEvaluationCount);

            viewModel.NextCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(WorkflowStep.Results, viewModel.CurrentStep);
            ResultsOutputView resultsView = Assert.Single(
                window.GetVisualDescendants().OfType<ResultsOutputView>());
            Assert.Same(results, resultsView.DataContext);
            Assert.True(Required<Border>(window, "EthicsWarningBanner").IsVisible);
            await results.ExportAsync(TestContext.Current.CancellationToken);
            Assert.Same(execution.LastRunContext, output.LastRequest?.Context);

            ExecutionRunContext completedContext = execution.LastRunContext
                ?? throw new InvalidOperationException("Completed run context is required.");
            viewModel.PreviousCommand.Execute(null);

            Assert.Equal(WorkflowStep.Execution, viewModel.CurrentStep);
            Assert.Same(completedContext, execution.LastRunContext);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Shell_does_not_reuse_previous_execution_configuration_after_input_is_unloaded()
    {
        using X02TemporaryWorkbook workbook = X02SyntheticWorkbookFactory.CreateSingleSheet(
            "Original",
            1,
            2,
            2,
            new X02Header(1, "Answer"));
        InputViewModel input = new();
        await input.SetFilePathAsync(workbook.Path, TestContext.Current.CancellationToken);
        ExecutionViewModel execution = U04TestSupport.ConfiguredExecutionViewModel(
            input.DefinitionDraft,
            input.Metadata ?? throw new InvalidOperationException("Workbook metadata is required."),
            new RecordingRunBoundary((_, _, _) => throw new InvalidOperationException("must not run")));
        MainWindowViewModel viewModel = new(
            new WorkflowNavigator(),
            input,
            new QuantificationDesignViewModel(input.DefinitionDraft),
            execution,
            new ResultsOutputViewModel(new RecordingOutputBoundary()));
        MainWindow window = new(viewModel);

        try
        {
            window.Show();
            viewModel.NextCommand.Execute(null);
            viewModel.NextCommand.Execute(null);
            Assert.True(execution.IsConfigured);

            viewModel.NavigateCommand.Execute(WorkflowStep.Input);
            input.SetFilePath(string.Empty);
            viewModel.NavigateCommand.Execute(WorkflowStep.Execution);

            Assert.False(execution.IsConfigured);
            Assert.False(execution.CanStart);
            Assert.Contains(
                execution.TechnicalErrors,
                error => error.Code == "EXECUTION_CONFIGURATION_REQUIRED");
        }
        finally
        {
            window.Close();
        }
    }

    private static T Required<T>(Control root, string name)
        where T : Control =>
        Assert.IsType<T>(root.FindControl<T>(name));

    private static void Press(TopLevel window, Key key)
    {
        PhysicalKey physicalKey = key switch
        {
            Key.Tab => PhysicalKey.Tab,
            Key.Enter => PhysicalKey.Enter,
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unsupported test key."),
        };
        window.KeyPress(key, RawInputModifiers.None, physicalKey, null);
        window.KeyRelease(key, RawInputModifiers.None, physicalKey, null);
        Dispatcher.UIThread.RunJobs();
    }
}