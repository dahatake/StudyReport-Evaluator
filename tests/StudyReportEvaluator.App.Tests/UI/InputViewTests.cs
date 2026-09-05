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
    public async Task Changing_primary_column_replaces_manual_question_text_and_removes_duplicate_supporting_column()
    {
        using X02TemporaryWorkbook workbook = X02SyntheticWorkbookFactory.CreateSampleLike();
        InputViewModel viewModel = new();
        await viewModel.SetFilePathAsync(workbook.Path, TestContext.Current.CancellationToken);
        InputQuestionMappingViewModel question = viewModel.Questions[0];
        SourceColumnOption selectedColumn = Assert.Single(
            viewModel.AvailableColumns,
            option => option.ColumnName == "G");

        question.QuestionText = "MANUAL-QUESTION-TEXT";
        question.SetSupportingColumn("G", selected: true);
        QuestionDefinition before = Assert.Single(
            viewModel.DefinitionDraft.Questions,
            item => item.Id == question.Id);

        question.PrimarySourceColumn = "G";

        QuestionDefinition updated = Assert.Single(
            viewModel.DefinitionDraft.Questions,
            item => item.Id == question.Id);
        Assert.Equal(selectedColumn.HeaderText, question.QuestionText);
        Assert.Equal(selectedColumn.HeaderText, updated.QuestionText);
        Assert.NotEqual("MANUAL-QUESTION-TEXT", updated.QuestionText);
        Assert.Equal("G", updated.PrimarySourceColumn);
        Assert.DoesNotContain("G", updated.SupportingSourceColumns, StringComparer.OrdinalIgnoreCase);
        Assert.False(Assert.Single(
            question.SupportingColumns,
            column => column.ColumnName == "G").IsSelected);
        Assert.Equal(before.Id, updated.Id);
        Assert.Equal(before.DisplayName, updated.DisplayName);
        Assert.Equal(before.Points, updated.Points);
        Assert.Equal(before.Enabled, updated.Enabled);
        Assert.Equal(before.Evaluators.Select(evaluator => evaluator.Id), updated.Evaluators.Select(evaluator => evaluator.Id));
        Assert.Equal(
            before.SpecialEvaluations.Select(special => special.Id),
            updated.SpecialEvaluations.Select(special => special.Id));
        Assert.True(viewModel.CanContinue);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Changing_primary_column_uses_the_loaded_question_text_row_for_supported_form_profiles(
        int questionTextRow)
    {
        await AssertPrimaryColumnQuestionTextAsync(
            X02SyntheticWorkbookFactory.CreateMicrosoftFormsLikeProfile((uint)questionTextRow),
            questionTextRow,
            initialColumn: "F",
            selectedColumn: "I",
            expectedQuestionText: "Report answer 2");
        await AssertPrimaryColumnQuestionTextAsync(
            X02SyntheticWorkbookFactory.CreateGoogleFormsLikeProfile((uint)questionTextRow),
            questionTextRow,
            initialColumn: "C",
            selectedColumn: "E",
            expectedQuestionText: "Report answer 2");
    }

    private static async Task AssertPrimaryColumnQuestionTextAsync(
        X02TemporaryWorkbook workbook,
        int questionTextRow,
        string initialColumn,
        string selectedColumn,
        string expectedQuestionText)
    {
        using (workbook)
        {
            InputViewModel viewModel = new()
            {
                HeaderRow = questionTextRow,
            };
            await viewModel.SetFilePathAsync(workbook.Path, TestContext.Current.CancellationToken);
            InputQuestionMappingViewModel question = Assert.Single(
                viewModel.Questions,
                item => item.PrimarySourceColumn == initialColumn);

            question.PrimarySourceColumn = selectedColumn;

            Assert.Equal((uint)questionTextRow, viewModel.Metadata!.HeaderRowNumber);
            Assert.Equal(expectedQuestionText, question.QuestionText);
            Assert.Equal(
                expectedQuestionText,
                Assert.Single(viewModel.DefinitionDraft.Questions, item => item.Id == question.Id).QuestionText);
            Assert.True(viewModel.CanContinue);
        }
    }

    [Fact]
    public async Task Selecting_a_column_without_a_header_clears_question_text_and_requires_manual_input()
    {
        using X02TemporaryWorkbook workbook = X02SyntheticWorkbookFactory.CreateSingleSheet(
            "Responses",
            headerRow: 1,
            lastRow: 4,
            lastColumn: 3,
            new X02Header(2, "Report answer"));
        InputViewModel viewModel = new();
        await viewModel.SetFilePathAsync(workbook.Path, TestContext.Current.CancellationToken);
        InputQuestionMappingViewModel question = Assert.Single(viewModel.Questions);
        Assert.Equal(string.Empty, Assert.Single(
            viewModel.AvailableColumns,
            option => option.ColumnName == "A").HeaderText);
        question.QuestionText = "MANUAL-QUESTION-TEXT";

        question.PrimarySourceColumn = "A";

        Assert.Equal(string.Empty, question.QuestionText);
        Assert.Equal(
            string.Empty,
            Assert.Single(viewModel.DefinitionDraft.Questions, item => item.Id == question.Id).QuestionText);
        InputValidationError error = Assert.Single(
            viewModel.ValidationErrors,
            item => item.Code == "REQUIRED"
                && item.NodeId == question.Id
                && item.Field == "QuestionText");
        Assert.Equal("REQUIRED", error.Code);
        Assert.False(viewModel.CanContinue);
    }

    [Fact]
    public async Task Changing_primary_column_while_header_metadata_is_stale_preserves_question_text()
    {
        using X02TemporaryWorkbook workbook = X02SyntheticWorkbookFactory.CreateSampleLike();
        InputViewModel viewModel = new();
        await viewModel.SetFilePathAsync(workbook.Path, TestContext.Current.CancellationToken);
        InputQuestionMappingViewModel question = viewModel.Questions[0];
        const string ManualQuestionText = "MANUAL-QUESTION-TEXT";
        question.QuestionText = ManualQuestionText;
        string staleHeaderText = Assert.Single(
            viewModel.AvailableColumns,
            option => option.ColumnName == "G").HeaderText;
        Assert.NotEqual(ManualQuestionText, staleHeaderText);

        viewModel.HeaderRow = 2;
        question.PrimarySourceColumn = "G";

        QuestionDefinition updated = Assert.Single(
            viewModel.DefinitionDraft.Questions,
            item => item.Id == question.Id);
        Assert.Equal("G", updated.PrimarySourceColumn);
        Assert.Equal(ManualQuestionText, question.QuestionText);
        Assert.Equal(ManualQuestionText, updated.QuestionText);
        Assert.DoesNotContain(
            viewModel.ValidationErrors,
            error => error.Code == "REQUIRED" && error.Field == "QuestionText");
        Assert.Contains(viewModel.ValidationErrors, error => error.Code == "HEADER_METADATA_MISMATCH");
        Assert.False(viewModel.CanContinue);
    }

    [AvaloniaFact]
    public async Task Selecting_primary_column_in_the_view_updates_the_visible_question_text()
    {
        using X02TemporaryWorkbook workbook = X02SyntheticWorkbookFactory.CreateSampleLike();
        InputViewModel viewModel = new();
        await viewModel.SetFilePathAsync(workbook.Path, TestContext.Current.CancellationToken);
        InputQuestionMappingViewModel question = viewModel.Questions[0];
        (string Id, string PrimarySourceColumn, string QuestionText)[] otherQuestionsBefore =
            viewModel.DefinitionDraft.Questions
                .Skip(1)
                .Select(item => (item.Id, item.PrimarySourceColumn, item.QuestionText))
                .ToArray();
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
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            ComboBox primaryColumn = Assert.Single(
                view.GetVisualDescendants().OfType<ComboBox>(),
                comboBox => AutomationProperties.GetAutomationId(comboBox) == question.PrimaryAutomationId);
            TextBox questionText = Assert.Single(
                view.GetVisualDescendants().OfType<TextBox>(),
                textBox => ReferenceEquals(textBox.DataContext, question)
                    && AutomationProperties.GetName(textBox) == "評価する設問 text");
            const string ExpectedQuestionText = "質問 1 の学生プロンプト";

            primaryColumn.SelectedItem = "G";
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            Assert.Equal("G", question.PrimarySourceColumn);
            Assert.Equal(ExpectedQuestionText, questionText.Text);
            Assert.Equal(
                ExpectedQuestionText,
                Assert.Single(viewModel.DefinitionDraft.Questions, item => item.Id == question.Id).QuestionText);
            Assert.Equal(
                otherQuestionsBefore,
                viewModel.DefinitionDraft.Questions
                    .Skip(1)
                    .Select(item => (item.Id, item.PrimarySourceColumn, item.QuestionText))
                    .ToArray());
        }
        finally
        {
            window.Close();
        }
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
            Assert.Equal(ScrollBarVisibility.Disabled, scroll.HorizontalScrollBarVisibility);
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

            Border scaledRange = Required<Border>(view, "InputRangeCard");
            Border scaledSuggestions = Required<Border>(view, "InputSuggestionCard");
            Assert.Equal(scaledRange.Bounds.Y, scaledSuggestions.Bounds.Y, 1d);
            Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 1d);

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

    [AvaloniaFact]
    public async Task Mapping_panes_split_by_available_width_instead_of_loaded_content_length()
    {
        using X02TemporaryWorkbook workbook = X02SyntheticWorkbookFactory.CreateSampleLike();
        InputViewModel viewModel = new();
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
            Render();

            Border range = Required<Border>(view, "InputRangeCard");
            Border suggestions = Required<Border>(view, "InputSuggestionCard");
            ScrollViewer scroll = Required<ScrollViewer>(view, "InputScrollViewer");
            double emptyRangeWidth = range.Bounds.Width;
            double emptySuggestionWidth = suggestions.Bounds.Width;

            await viewModel.SetFilePathAsync(workbook.Path, TestContext.Current.CancellationToken);
            Render();

            Assert.NotEmpty(viewModel.MappingSuggestions);
            Assert.Equal(emptyRangeWidth, range.Bounds.Width, 1d);
            Assert.Equal(emptySuggestionWidth, suggestions.Bounds.Width, 1d);
            Assert.Equal(0, Grid.GetRow(range));
            Assert.Equal(0, Grid.GetColumn(range));
            Assert.Equal(0, Grid.GetRow(suggestions));
            Assert.Equal(1, Grid.GetColumn(suggestions));
            Assert.Equal(range.Bounds.Y, suggestions.Bounds.Y, 1d);
            Assert.Equal(18d, suggestions.Bounds.X - range.Bounds.Right, 1d);
            Assert.Equal(1.5d, suggestions.Bounds.Width / range.Bounds.Width, 2);
            Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 1d);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Mapping_panes_stack_vertically_when_the_view_is_too_narrow_for_two_columns()
    {
        using X02TemporaryWorkbook workbook = X02SyntheticWorkbookFactory.CreateSampleLike();
        InputViewModel viewModel = new();
        await viewModel.SetFilePathAsync(workbook.Path, TestContext.Current.CancellationToken);
        InputView view = new(viewModel);
        Window window = new()
        {
            Width = 720,
            Height = 900,
            Content = view,
        };

        try
        {
            window.Show();
            Render();

            Border range = Required<Border>(view, "InputRangeCard");
            Border suggestions = Required<Border>(view, "InputSuggestionCard");
            ScrollViewer scroll = Required<ScrollViewer>(view, "InputScrollViewer");

            Assert.Equal(range.Bounds.Width, suggestions.Bounds.Width, 1d);
            Assert.Equal(0, Grid.GetRow(range));
            Assert.Equal(2, Grid.GetColumnSpan(range));
            Assert.Equal(1, Grid.GetRow(suggestions));
            Assert.Equal(2, Grid.GetColumnSpan(suggestions));
            Assert.True(suggestions.Bounds.Y >= range.Bounds.Bottom);
            Assert.Equal(ScrollBarVisibility.Disabled, scroll.HorizontalScrollBarVisibility);
            Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 1d);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Mapping_panes_switch_at_the_documented_880_dip_container_boundary()
    {
        InputView view = new(new InputViewModel());
        Window window = new()
        {
            Width = 1180,
            Height = 3000,
            Content = view,
        };

        try
        {
            window.Show();
            Render();

            Grid root = Required<Grid>(view, "InputContentRoot");
            Border range = Required<Border>(view, "InputRangeCard");
            Border suggestions = Required<Border>(view, "InputSuggestionCard");
            double widthOutsideContainer = window.ClientSize.Width - root.Bounds.Width;

            window.Width = 879d + widthOutsideContainer;
            Render();
            Assert.InRange(root.Bounds.Width, 878.5d, 879.5d);
            Assert.Equal(1, Grid.GetRow(suggestions));
            Assert.Equal(2, Grid.GetColumnSpan(range));

            window.Width = 880d + widthOutsideContainer;
            Render();
            Assert.InRange(root.Bounds.Width, 879.5d, 880.5d);
            Assert.Equal(0, Grid.GetRow(suggestions));
            Assert.Equal(1, Grid.GetColumn(suggestions));
            Assert.Equal(1, Grid.GetColumnSpan(range));

            window.Width = 881d + widthOutsideContainer;
            Render();
            Assert.InRange(root.Bounds.Width, 880.5d, 881.5d);
            Assert.Equal(0, Grid.GetRow(suggestions));
            Assert.Equal(1, Grid.GetColumn(suggestions));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Long_unbreakable_header_text_does_not_widen_the_input_layout()
    {
        const string LongHeader =
            "ABCDEFGHIJKLMNOPQRSTUVWXYZABCDEFGHIJKLMNOPQRSTUVWXYZABCDEFGHIJKLMNOPQRSTUVWXYZABCDEFGHIJ";
        using X02TemporaryWorkbook workbook = X02SyntheticWorkbookFactory.CreateSingleSheet(
            "Original",
            headerRow: 1,
            lastRow: 12,
            lastColumn: 2,
            new X02Header(1, LongHeader),
            new X02Header(2, LongHeader + "-supporting"));
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
            Render();

            Assert.Contains(viewModel.AvailableColumns, column => column.HeaderText == LongHeader);
            Border range = Required<Border>(view, "InputRangeCard");
            Border suggestions = Required<Border>(view, "InputSuggestionCard");
            ScrollViewer scroll = Required<ScrollViewer>(view, "InputScrollViewer");

            Assert.Equal(1.5d, suggestions.Bounds.Width / range.Bounds.Width, 2);
            Assert.True(suggestions.Bounds.Right <= view.Bounds.Width + 1d);
            Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 1d);
        }
        finally
        {
            window.Close();
        }
    }

    private static T Required<T>(Control root, string name)
        where T : Control =>
        Assert.IsType<T>(root.FindControl<T>(name));

    private static void Render()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
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