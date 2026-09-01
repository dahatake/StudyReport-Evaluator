using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using StudyReportEvaluator.App.Tests.Workflow;
using StudyReportEvaluator.App.Tests.Workbooks.Mapping;
using StudyReportEvaluator.App.ViewModels;
using StudyReportEvaluator.App.Views;
using StudyReportEvaluator.App.Workflow;
using StudyReportEvaluator.App.Workbooks.Reading;
using StudyReportEvaluator.Core.Domain;
using Xunit;

namespace StudyReportEvaluator.App.Tests.UI;

public sealed class PrimaryJourneyAccessibilityTests
{
    [AvaloniaFact]
    public async Task Standalone_execution_is_labelled_keyboard_focusable_and_scrollable_at_two_hundred_percent()
    {
        QuantificationDefinition definition = U04TestSupport.Definition(2, 3);
        WorkbookMetadata metadata = U01TestSupport.ValidateMapping(definition).Metadata;
        RunSummary summary = await U04TestSupport.CreateSummaryAsync(definition, metadata);
        ExecutionViewModel viewModel = U04TestSupport.ConfiguredExecutionViewModel(
            definition,
            metadata,
            new RecordingRunBoundary((_, _, _) => Task.FromResult(summary)));
        await viewModel.CheckAuthenticationAsync(TestContext.Current.CancellationToken);
        ExecutionView view = new(viewModel);
        Window window = new()
        {
            Width = 1080,
            Height = 760,
            Content = view,
        };

        try
        {
            window.Show();
            window.SetRenderScaling(2d);
            Dispatcher.UIThread.RunJobs();

            ScrollViewer scroll = Required<ScrollViewer>(view, "ExecutionScrollViewer");
            Button auth = Required<Button>(view, "CheckAuthenticationButton");
            ComboBox model = Required<ComboBox>(view, "ModelComboBox");
            ComboBox concurrency = Required<ComboBox>(view, "ConcurrencyComboBox");
            CheckBox resumeMode = Required<CheckBox>(view, "ResumeModeCheckBox");
            TextBox outputDirectory = Required<TextBox>(view, "OutputDirectoryTextBox");
            Button start = Required<Button>(view, "StartRunButton");
            Button cancel = Required<Button>(view, "CancelRunButton");
            ProgressBar progress = Required<ProgressBar>(view, "RunProgressBar");
            Border validation = Required<Border>(view, "ExecutionValidationSummary");

            Assert.Same(viewModel, view.DataContext);
            Assert.Equal(2d, window.RenderScaling);
            Assert.Equal(ScrollBarVisibility.Auto, scroll.HorizontalScrollBarVisibility);
            Assert.Equal(ScrollBarVisibility.Auto, scroll.VerticalScrollBarVisibility);
            Assert.Equal("CheckCopilotAuthentication", AutomationProperties.GetAutomationId(auth));
            Assert.Equal("ExecutionModel", AutomationProperties.GetAutomationId(model));
            Assert.Equal("ExecutionConcurrency", AutomationProperties.GetAutomationId(concurrency));
            Assert.Equal("ExecutionResumeMode", AutomationProperties.GetAutomationId(resumeMode));
            Assert.Equal("ExecutionOutputDirectory", AutomationProperties.GetAutomationId(outputDirectory));
            Assert.Equal("StartQuantification", AutomationProperties.GetAutomationId(start));
            Assert.Equal("CancelQuantification", AutomationProperties.GetAutomationId(cancel));
            Assert.Equal("ExecutionProgress", AutomationProperties.GetAutomationId(progress));
            Assert.Equal("ExecutionValidationSummary", AutomationProperties.GetAutomationId(validation));
            Assert.All(
                new Control[] { auth, model, concurrency, resumeMode, outputDirectory, start, cancel, progress },
                control => Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(control))));
            Assert.Same(auth, window.FocusManager?.GetFocusedElement());
            Assert.True(auth.MinHeight >= 44d);
            Assert.True(start.MinHeight >= 44d);
            Assert.True(resumeMode.MinHeight >= 44d);
            Assert.True(outputDirectory.MinHeight >= 44d);
            Assert.True(start.IsEffectivelyEnabled);
            Assert.False(cancel.IsEffectivelyEnabled);
            Assert.Null(view.FindControl<Border>("EthicsWarningBanner"));

            Press(window, Key.Tab);
            Assert.NotSame(auth, window.FocusManager?.GetFocusedElement());
            Assert.True(start.Focus(NavigationMethod.Tab, KeyModifiers.None));
            Assert.Same(start, window.FocusManager?.GetFocusedElement());
            Assert.Equal(new Thickness(3d), start.BorderThickness);
            Assert.True(model.Focus(NavigationMethod.Tab, KeyModifiers.None));
            Assert.Equal(new Thickness(3d), model.BorderThickness);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Standalone_results_is_keyboard_operable_and_warning_gate_free_at_two_hundred_percent_scroll()
    {
        QuantificationDefinition definition = U04TestSupport.Definition(2, 2);
        WorkbookMetadata metadata = U01TestSupport.ValidateMapping(definition).Metadata;
        RunSummary summary = await U04TestSupport.CreateSummaryAsync(definition, metadata);
        RecordingOutputBoundary output = new();
        ResultsOutputViewModel viewModel = new(output, U04TestSupport.Context(summary));
        ResultsOutputView view = new(viewModel);
        Window window = new()
        {
            Width = 1280,
            Height = 850,
            Content = view,
        };

        try
        {
            window.Show();
            window.SetRenderScaling(2d);
            Dispatcher.UIThread.RunJobs();

            ScrollViewer scroll = Required<ScrollViewer>(view, "ResultsOutputScrollViewer");
            ListBox results = Required<ListBox>(view, "ResultsList");
            ListBox rowScores = Required<ListBox>(view, "RowScoreList");
            TextBox outputPath = Required<TextBox>(view, "OutputPathTextBox");
            Button export = Required<Button>(view, "ExportButton");
            Button cancel = Required<Button>(view, "CancelExportButton");
            ResultsCriterionViewModel item = Assert.Single(viewModel.Results);
            TextBox overrideEditor = Assert.Single(
                view.GetVisualDescendants().OfType<TextBox>(),
                textBox => AutomationProperties.GetAutomationId(textBox) == item.AutomationId + "-Override");

            Assert.Same(viewModel, view.DataContext);
            Assert.Equal(2d, window.RenderScaling);
            Assert.Equal(ScrollBarVisibility.Auto, scroll.HorizontalScrollBarVisibility);
            Assert.Equal(ScrollBarVisibility.Auto, scroll.VerticalScrollBarVisibility);
            Assert.Equal("ResultsReviewList", AutomationProperties.GetAutomationId(results));
            Assert.Equal("ResultsRowScores", AutomationProperties.GetAutomationId(rowScores));
            Assert.Equal("ResultsOutputPath", AutomationProperties.GetAutomationId(outputPath));
            Assert.Equal("ExportQuantifiedWorkbook", AutomationProperties.GetAutomationId(export));
            Assert.Equal("CancelWorkbookOutput", AutomationProperties.GetAutomationId(cancel));
            Assert.True(view.GetVisualDescendants().OfType<VirtualizingStackPanel>().Any());
            Assert.True(viewModel.CanExport);
            Assert.True(export.IsEffectivelyEnabled);
            Assert.False(cancel.IsEffectivelyEnabled);
            Assert.Same(results, window.FocusManager?.GetFocusedElement());
            Assert.True(outputPath.MinHeight >= 44d);
            Assert.True(export.MinHeight >= 44d);
            Assert.Null(view.FindControl<Border>("EthicsWarningBanner"));
            AssertUniqueAutomationIds(view);

            Assert.True(overrideEditor.Focus(NavigationMethod.Tab, KeyModifiers.None));
            Assert.Same(overrideEditor, window.FocusManager?.GetFocusedElement());
            Assert.Equal(new Thickness(3d), overrideEditor.BorderThickness);
            Press(window, Key.Tab);
            Assert.NotSame(overrideEditor, window.FocusManager?.GetFocusedElement());
            Assert.True(export.Focus(NavigationMethod.Tab, KeyModifiers.None));
            Assert.Equal(new Thickness(3d), export.BorderThickness);
            Assert.True(viewModel.CanExport);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Large_results_virtualize_rows_instead_of_realizing_every_override_editor()
    {
        QuantificationDefinition definition = U04TestSupport.Definition(2, 531);
        WorkbookMetadata metadata = U01TestSupport.ValidateMapping(definition).Metadata;
        RunSummary summary = await U04TestSupport.CreateSummaryAsync(definition, metadata);
        ResultsOutputViewModel viewModel = new(
            new RecordingOutputBoundary(),
            U04TestSupport.Context(summary));
        ResultsOutputView view = new(viewModel);
        Window window = new()
        {
            Width = 1280,
            Height = 850,
            Content = view,
        };

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(530, viewModel.Results.Count);
            Assert.Contains(view.GetVisualDescendants(), descendant => descendant is VirtualizingStackPanel);
            Assert.InRange(
                view.GetVisualDescendants().OfType<TextBox>().Count(textBox =>
                    (AutomationProperties.GetAutomationId(textBox) ?? string.Empty).EndsWith(
                        "-Override",
                        StringComparison.Ordinal)),
                1,
                30);
        }
        finally
        {
            window.Close();
        }
    }

    private static T Required<T>(Control root, string name)
        where T : Control =>
        Assert.IsType<T>(root.FindControl<T>(name));

    private static void AssertUniqueAutomationIds(Control root)
    {
        string[] ids = root.GetVisualDescendants()
            .OfType<Control>()
            .Select(AutomationProperties.GetAutomationId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .ToArray();
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
    }

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