using System.Reflection;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using StudyReportEvaluator.App.Tests.Workbooks.Mapping;
using StudyReportEvaluator.App.ViewModels;
using StudyReportEvaluator.App.Views;
using StudyReportEvaluator.App.Workbooks.Intake;
using StudyReportEvaluator.App.Workbooks.Reading;
using StudyReportEvaluator.Core.Domain;
using Xunit;

namespace StudyReportEvaluator.App.Tests.UI;

public sealed class InputViewTests
{
    [Fact]
    public async Task Sample_like_workbook_loads_asynchronously_and_exposes_overridable_F_through_K_suggestions()
    {
        using X02TemporaryWorkbook workbook = X02SyntheticWorkbookFactory.CreateSampleLike();
        CountingInputLoader loader = new();
        InputViewModel viewModel = new(loader);

        await viewModel.SetFilePathAsync(workbook.Path, TestContext.Current.CancellationToken);

        Assert.Equal(1, loader.LoadCount);
        Assert.True(viewModel.HasLoadedWorkbook);
        Assert.NotNull(viewModel.Snapshot);
        Assert.NotNull(viewModel.Metadata);
        Assert.Equal("Original", viewModel.SelectedSheet);
        Assert.Equal(1, viewModel.HeaderRow);
        Assert.Equal(2, viewModel.FirstDataRow);
        Assert.Equal(531, viewModel.LastDataRow);
        Assert.Equal(["F", "G", "H", "I", "J", "K"], viewModel.MappingSuggestions.Select(item => item.ColumnName));
        Assert.Equal(["F", "G", "H", "I", "J"], viewModel.Questions.Select(item => item.PrimarySourceColumn));

        MappingSuggestionViewModel studentPrompt = Assert.Single(
            viewModel.MappingSuggestions,
            item => item.ColumnName == "J");
        Assert.True(studentPrompt.IsStudentPromptPrimaryCandidate);
        Assert.Equal("K", studentPrompt.SuggestedSupportingColumns);
        InputQuestionMappingViewModel promptQuestion = Assert.Single(
            viewModel.Questions,
            item => item.PrimarySourceColumn == "J");
        Assert.Equal("K", Assert.Single(
            promptQuestion.SupportingColumns,
            item => item.IsSelected).ColumnName);
        Assert.Equal(EvaluatorType.CustomPrompt, Assert.Single(
            viewModel.DefinitionDraft.Questions,
            item => item.Id == promptQuestion.Id).Evaluators[0].Type);
        Assert.True(viewModel.IsUsingSuggestedMapping);
        Assert.True(viewModel.CanContinue);
        Assert.Empty(viewModel.ValidationErrors);

        QuantificationDefinition suggestedDraft = viewModel.DefinitionDraft;
        InputQuestionMappingViewModel first = viewModel.Questions[0];
        first.PrimarySourceColumn = "G";
        first.SetSupportingColumn("H", selected: true);
        viewModel.FirstDataRow = 3;
        viewModel.LastDataRow = 500;

        Assert.NotSame(suggestedDraft, viewModel.DefinitionDraft);
        Assert.Equal("F", suggestedDraft.Questions[0].PrimarySourceColumn);
        Assert.Equal("G", viewModel.DefinitionDraft.Questions[0].PrimarySourceColumn);
        Assert.Contains("H", viewModel.DefinitionDraft.Questions[0].SupportingSourceColumns);
        Assert.Equal(3, viewModel.DefinitionDraft.FirstDataRow);
        Assert.Equal(500, viewModel.DefinitionDraft.LastDataRow);
        Assert.False(viewModel.IsUsingSuggestedMapping);
        Assert.True(viewModel.CanContinue);
        Assert.True(viewModel.TryCreateDesignDefinition(out QuantificationDefinition? designSeed));
        Assert.NotNull(designSeed);
        Assert.NotSame(viewModel.DefinitionDraft, designSeed);
        Assert.Equal("G", designSeed!.Questions[0].PrimarySourceColumn);

        first.SetSupportingColumn("G", selected: true);
        Assert.False(viewModel.CanContinue);
        Assert.Contains(viewModel.ValidationErrors, error => error.Code == "PRIMARY_COLUMN_REUSED");
        first.SetSupportingColumn("G", selected: false);
        Assert.True(viewModel.CanContinue);

        first.SetSupportingColumn("H", selected: true);
        first.PrimarySourceColumn = "H";
        Assert.DoesNotContain("H", viewModel.DefinitionDraft.Questions[0].SupportingSourceColumns);
        Assert.False(Assert.Single(
            first.SupportingColumns,
            column => column.ColumnName == "H").IsSelected);
        Assert.True(viewModel.CanContinue);

        Assert.DoesNotContain(workbook.Path, viewModel.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.All(
            viewModel.ValidationErrors,
            error => Assert.DoesNotContain(workbook.Path, error.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Editing_the_file_path_invalidates_loaded_metadata_snapshot_and_suggestions()
    {
        using X02TemporaryWorkbook workbook = X02SyntheticWorkbookFactory.CreateSampleLike();
        InputViewModel viewModel = new();
        await viewModel.SetFilePathAsync(workbook.Path, TestContext.Current.CancellationToken);
        Assert.True(viewModel.CanContinue);

        viewModel.FilePath = workbook.Path + ".replacement.xlsx";

        Assert.False(viewModel.HasLoadedWorkbook);
        Assert.Null(viewModel.Metadata);
        Assert.Null(viewModel.Snapshot);
        Assert.Empty(viewModel.Worksheets);
        Assert.Empty(viewModel.MappingSuggestions);
        Assert.Empty(viewModel.Questions);
        Assert.False(viewModel.IsUsingSuggestedMapping);
        Assert.False(viewModel.CanContinue);
        Assert.Contains(viewModel.ValidationErrors, error => error.Code == "INPUT_WORKBOOK_REQUIRED");
    }

    [Fact]
    public async Task Editing_the_file_path_supersedes_an_in_flight_load_without_adopting_stale_results()
    {
        using X02TemporaryWorkbook workbook = X02SyntheticWorkbookFactory.CreateSampleLike();
        DelayedInputLoader loader = new();
        InputViewModel viewModel = new(loader);
        viewModel.FilePath = workbook.Path;
        Task load = viewModel.LoadAsync(TestContext.Current.CancellationToken);
        await loader.Started;
        Assert.True(viewModel.IsBusy);

        viewModel.FilePath = workbook.Path + ".replacement.xlsx";
        loader.Release();
        await load;

        Assert.False(viewModel.IsBusy);
        Assert.False(viewModel.HasLoadedWorkbook);
        Assert.Null(viewModel.Metadata);
        Assert.Null(viewModel.Snapshot);
        Assert.False(viewModel.CanContinue);
    }

    [AvaloniaFact]
    public async Task Native_picker_selection_uses_the_same_read_only_loader_and_cancel_preserves_state()
    {
        using X02TemporaryWorkbook workbook = X02SyntheticWorkbookFactory.CreateSampleLike();
        CountingInputLoader loader = new();
        InputViewModel viewModel = new(loader);
        ScriptedInputWorkbookPicker picker = new(workbook.Path, null);
        InputView view = new(viewModel, picker);
        Window window = new() { Content = view };

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            await view.PickFileAsync();

            Assert.Equal(Path.GetFullPath(workbook.Path), Path.GetFullPath(viewModel.FilePath));
            Assert.True(viewModel.HasLoadedWorkbook);
            Assert.True(viewModel.CanContinue);
            Assert.Equal(1, loader.LoadCount);
            InputSnapshot? snapshot = viewModel.Snapshot;
            WorkbookMetadata? metadata = viewModel.Metadata;

            await view.PickFileAsync();

            Assert.Equal(2, picker.CallCount);
            Assert.Equal(1, loader.LoadCount);
            Assert.Same(snapshot, viewModel.Snapshot);
            Assert.Same(metadata, viewModel.Metadata);
            Assert.Equal(workbook.Path, viewModel.FilePath);
            Button pick = Required<Button>(view, "PickFileButton");
            Assert.True(pick.IsEnabled);
            Assert.Equal("PickInputWorkbook", AutomationProperties.GetAutomationId(pick));
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void Question_text_row_choices_are_closed_to_one_or_two()
    {
        InputViewModel viewModel = new();

        Assert.Equal([1, 2], viewModel.HeaderRowOptions);
        viewModel.HeaderRow = 2;
        Assert.Equal(2, viewModel.HeaderRow);
        Assert.Throws<ArgumentOutOfRangeException>(() => viewModel.HeaderRow = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => viewModel.HeaderRow = 3);
        Assert.Equal(2, viewModel.HeaderRow);
    }

    [Fact]
    public async Task Changing_question_text_row_preserves_manual_points_and_requires_explicit_header_reload()
    {
        using X02TemporaryWorkbook workbook = X02SyntheticWorkbookFactory.CreateSampleLike();
        InputViewModel viewModel = new();
        await viewModel.SetFilePathAsync(workbook.Path, TestContext.Current.CancellationToken);
        decimal[] points = viewModel.DefinitionDraft.Questions.Select(question => question.Points).ToArray();

        viewModel.HeaderRow = 2;

        Assert.Equal(points, viewModel.DefinitionDraft.Questions.Select(question => question.Points));
        Assert.False(viewModel.CanContinue);
        Assert.Contains(viewModel.ValidationErrors, error => error.Code == "HEADER_METADATA_MISMATCH");
    }

    [AvaloniaFact]
    public async Task Picker_local_path_unavailable_is_distinguished_from_cancel_without_disclosing_content()
    {
        InputViewModel viewModel = new();
        InputView view = new(viewModel, new ThrowingInputWorkbookPicker());
        Window window = new() { Content = view };

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            await view.PickFileAsync();

            TextBlock status = Required<TextBlock>(view, "PickerStatusMessage");
            Assert.True(status.IsVisible);
            Assert.Contains("pathを直接入力", status.Text, StringComparison.Ordinal);
            Assert.DoesNotContain("PRIVATE", status.Text, StringComparison.Ordinal);
            Assert.Equal(string.Empty, viewModel.FilePath);
            Assert.False(viewModel.HasLoadedWorkbook);
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public async Task Question_mapping_add_copy_reorder_disable_delete_always_creates_a_new_draft()
    {
        using X02TemporaryWorkbook workbook = X02SyntheticWorkbookFactory.CreateSampleLike();
        InputViewModel viewModel = new();
        await viewModel.SetFilePathAsync(workbook.Path, TestContext.Current.CancellationToken);

        QuantificationDefinition beforeAdd = viewModel.DefinitionDraft;
        InputQuestionMappingViewModel added = viewModel.AddQuestion();
        Assert.NotSame(beforeAdd, viewModel.DefinitionDraft);
        Assert.Contains(viewModel.Questions, item => item.Id == added.Id);

        QuantificationDefinition beforeCopy = viewModel.DefinitionDraft;
        InputQuestionMappingViewModel copy = viewModel.DuplicateQuestion(added.Id);
        Assert.NotSame(beforeCopy, viewModel.DefinitionDraft);
        Assert.NotEqual(added.Id, copy.Id);
        Assert.NotEqual(
            Assert.Single(beforeCopy.Questions, item => item.Id == added.Id).Evaluators[0].Id,
            Assert.Single(viewModel.DefinitionDraft.Questions, item => item.Id == copy.Id).Evaluators[0].Id);

        viewModel.MoveQuestionUp(copy.Id);
        int copyIndex = viewModel.Questions.IndexOf(copy);
        Assert.True(copyIndex >= 0);
        Assert.Equal(copy.Id, viewModel.DefinitionDraft.Questions[copyIndex].Id);

        copy.Enabled = false;
        Assert.False(Assert.Single(viewModel.DefinitionDraft.Questions, item => item.Id == copy.Id).Enabled);
        Assert.Contains(viewModel.Questions, item => item.Id == copy.Id);
        viewModel.DeleteQuestion(copy.Id);
        Assert.DoesNotContain(viewModel.Questions, item => item.Id == copy.Id);
        Assert.DoesNotContain(viewModel.DefinitionDraft.Questions, item => item.Id == copy.Id);
        Assert.True(viewModel.CanContinue);
    }

    [Fact]
    public async Task Rejected_input_is_a_blocking_technical_error_without_path_or_content_disclosure()
    {
        string directory = Path.Combine(Path.GetTempPath(), "StudyReportEvaluator-U03-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "PRIVATE-CANARY.csv");
        await File.WriteAllTextAsync(
            path,
            "PRIVATE-CONTENT-CANARY",
            TestContext.Current.CancellationToken);
        try
        {
            InputViewModel viewModel = new();

            await viewModel.SetFilePathAsync(path, TestContext.Current.CancellationToken);

            Assert.False(viewModel.HasLoadedWorkbook);
            Assert.False(viewModel.CanContinue);
            InputValidationError error = Assert.Single(viewModel.ValidationErrors);
            Assert.Equal("CommaSeparatedValues", error.Code);
            Assert.DoesNotContain(path, error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("PRIVATE-CANARY", error.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("PRIVATE-CONTENT-CANARY", error.ToString(), StringComparison.Ordinal);
            Assert.Throws<InvalidOperationException>(() => viewModel.CreateDesignDefinition());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Input_contract_has_no_warning_acknowledgement_or_native_dialog_dependency()
    {
        string[] prohibitedTerms =
        [
            "Warning",
            "Ethics",
            "Acknowledge",
            "Consent",
            "Dismiss",
            "OpenFileDialog",
            "StorageProvider",
        ];
        MemberInfo[] members = typeof(InputViewModel).GetMembers(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        Assert.DoesNotContain(
            members,
            member => prohibitedTerms.Any(term => member.Name.Contains(term, StringComparison.OrdinalIgnoreCase)));
        Assert.NotNull(typeof(InputViewModel).GetConstructor([typeof(IInputWorkbookLoader)]));
        Assert.NotNull(typeof(InputViewModel).GetMethod(nameof(InputViewModel.SetFilePath)));
        Assert.NotNull(typeof(InputViewModel).GetMethod(nameof(InputViewModel.SetFilePathAsync)));
    }

    [AvaloniaFact]
    public async Task Standalone_input_view_is_labelled_keyboard_focusable_virtualized_and_scrollable_at_two_hundred_percent()
    {
        using X02TemporaryWorkbook workbook = X02SyntheticWorkbookFactory.CreateSampleLike();
        InputViewModel viewModel = new();
        await viewModel.SetFilePathAsync(workbook.Path, TestContext.Current.CancellationToken);
        InputView view = new(viewModel);
        Window window = new()
        {
            Width = 1180,
            Height = 820,
            Content = view,
        };

        try
        {
            window.Show();
            window.SetRenderScaling(2d);
            Dispatcher.UIThread.RunJobs();

            TextBox filePath = Required<TextBox>(view, "FilePathTextBox");
            Button load = Required<Button>(view, "LoadFileButton");
            Button pick = Required<Button>(view, "PickFileButton");
            ComboBox headerRow = Required<ComboBox>(view, "HeaderRowComboBox");
            ScrollViewer scroll = Required<ScrollViewer>(view, "InputScrollViewer");
            ListBox suggestions = Required<ListBox>(view, "MappingSuggestionList");
            ListBox questions = Required<ListBox>(view, "InputQuestionList");
            Border validation = Required<Border>(view, "InputValidationSummary");
            InputQuestionMappingViewModel firstQuestion = viewModel.Questions[0];
            TextBox questionName = Assert.Single(
                view.GetVisualDescendants().OfType<TextBox>(),
                textBox => AutomationProperties.GetAutomationId(textBox) == firstQuestion.NameAutomationId);
            ComboBox primaryColumn = Assert.Single(
                view.GetVisualDescendants().OfType<ComboBox>(),
                comboBox => AutomationProperties.GetAutomationId(comboBox) == firstQuestion.PrimaryAutomationId);

            Assert.Same(viewModel, view.DataContext);
            Assert.Equal(2d, window.RenderScaling);
            Assert.Equal(ScrollBarVisibility.Auto, scroll.HorizontalScrollBarVisibility);
            Assert.Equal(ScrollBarVisibility.Auto, scroll.VerticalScrollBarVisibility);
            Assert.Equal("InputFilePath", AutomationProperties.GetAutomationId(filePath));
            Assert.Equal("LoadInputWorkbook", AutomationProperties.GetAutomationId(load));
            Assert.Equal("PickInputWorkbook", AutomationProperties.GetAutomationId(pick));
            Assert.Equal("InputHeaderRow", AutomationProperties.GetAutomationId(headerRow));
            Assert.Equal([1, 2], headerRow.Items.Cast<int>());
            Assert.Equal("InputMappingSuggestions", AutomationProperties.GetAutomationId(suggestions));
            Assert.Equal("InputQuestionMappings", AutomationProperties.GetAutomationId(questions));
            Assert.Equal("InputValidationSummary", AutomationProperties.GetAutomationId(validation));
            Assert.NotEqual(
                AutomationProperties.GetAutomationId(questionName),
                AutomationProperties.GetAutomationId(primaryColumn));
            Assert.True(filePath.MinHeight >= 44d);
            Assert.True(pick.MinHeight >= 44d);
            Assert.True(load.MinHeight >= 44d);
            Assert.Same(filePath, window.FocusManager?.GetFocusedElement());

            Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "ファイル path（標準 .xlsx）");
            Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "主回答列");
            Assert.Contains(view.GetVisualDescendants(), descendant => descendant is VirtualizingStackPanel);
            Assert.Null(view.FindControl<Border>("EthicsWarningBanner"));

            Press(window, Key.Tab);
            Assert.NotSame(filePath, window.FocusManager?.GetFocusedElement());
            Assert.True(load.Focus(NavigationMethod.Tab, KeyModifiers.None));
            Assert.Same(load, window.FocusManager?.GetFocusedElement());
            Assert.Equal(new Thickness(3d), load.BorderThickness);
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

    private sealed class CountingInputLoader : IInputWorkbookLoader
    {
        private readonly InputWorkbookLoader inner = new();

        public int LoadCount { get; private set; }

        public Task<InputWorkbookLoadResult> LoadAsync(
            string filePath,
            uint headerRow,
            CancellationToken cancellationToken = default)
        {
            LoadCount++;
            return inner.LoadAsync(filePath, headerRow, cancellationToken);
        }
    }

    private sealed class DelayedInputLoader : IInputWorkbookLoader
    {
        private readonly InputWorkbookLoader inner = new();
        private readonly TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Started => started.Task;

        public void Release() => release.TrySetResult();

        public async Task<InputWorkbookLoadResult> LoadAsync(
            string filePath,
            uint headerRow,
            CancellationToken cancellationToken = default)
        {
            started.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
            return await inner.LoadAsync(filePath, headerRow, cancellationToken);
        }
    }

    private sealed class ScriptedInputWorkbookPicker(params string?[] paths) : IInputWorkbookPicker
    {
        private readonly Queue<string?> values = new(paths);

        public int CallCount { get; private set; }

        public Task<string?> PickAsync(TopLevel topLevel)
        {
            ArgumentNullException.ThrowIfNull(topLevel);
            CallCount++;
            return Task.FromResult(values.Count > 0 ? values.Dequeue() : null);
        }
    }

    private sealed class ThrowingInputWorkbookPicker : IInputWorkbookPicker
    {
        public Task<string?> PickAsync(TopLevel topLevel) =>
            throw new InputWorkbookPickerPathUnavailableException();
    }
}