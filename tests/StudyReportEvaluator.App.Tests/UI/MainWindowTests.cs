using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using StudyReportEvaluator.App.Composition;
using StudyReportEvaluator.App.Navigation;
using StudyReportEvaluator.App.ViewModels;
using StudyReportEvaluator.App.Views;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(StudyReportEvaluator.App.Tests.UI.U02TestAppBuilder))]

namespace StudyReportEvaluator.App.Tests.UI;

public sealed class U02TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
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
            Assert.True(warning.IsVisible);
            Assert.Same(input, window.FocusManager?.GetFocusedElement());
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

            Assert.True(next.Focus(NavigationMethod.Tab, KeyModifiers.None));
            Press(window, Key.Enter);
            Assert.Equal(WorkflowStep.Execution, window.ViewModel.CurrentStep);
            Press(window, Key.Enter);
            Assert.Equal(WorkflowStep.Results, window.ViewModel.CurrentStep);
            Assert.Equal("完了", window.ViewModel.ExecutionStep.StatusText);
            Assert.Contains("現在", window.ViewModel.ResultsStep.StatusText, StringComparison.Ordinal);
            Assert.False(next.IsEffectivelyEnabled);
            Assert.True(Required<Border>(window, "EthicsWarningBanner").IsVisible);
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
            Assert.True(Required<Border>(window, "CurrentStepPlaceholder").IsVisible);
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