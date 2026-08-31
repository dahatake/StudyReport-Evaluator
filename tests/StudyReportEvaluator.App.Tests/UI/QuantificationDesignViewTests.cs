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
using StudyReportEvaluator.App.ViewModels;
using StudyReportEvaluator.App.Views;
using StudyReportEvaluator.Core.Domain;
using StudyReportEvaluator.Core.Prompting;
using Xunit;

namespace StudyReportEvaluator.App.Tests.UI;

public sealed class QuantificationDesignViewTests
{
    [Fact]
    public void Dynamic_question_evaluator_and_criterion_operations_preserve_stable_ids_and_snapshot_isolation()
    {
        QuantificationDesignViewModel viewModel = new();
        Assert.True(viewModel.IsValid);
        QuestionDesignItemViewModel originalQuestion = Assert.Single(viewModel.Questions);
        EvaluatorDesignItemViewModel originalEvaluator = Assert.Single(originalQuestion.Evaluators);
        CriterionDesignItemViewModel originalCriterion = Assert.Single(originalEvaluator.Criteria);
        QuantificationSnapshot activeSnapshot = viewModel.BuildSnapshot();
        QuantificationDefinition beforeMutation = viewModel.Draft;

        QuestionDesignItemViewModel questionCopy = viewModel.DuplicateQuestion(originalQuestion.Id);
        Assert.NotSame(beforeMutation, viewModel.Draft);
        Assert.NotEqual(originalQuestion.Id, questionCopy.Id);
        Assert.NotEqual(originalEvaluator.Id, Assert.Single(questionCopy.Evaluators).Id);
        Assert.NotEqual(originalCriterion.Id, Assert.Single(Assert.Single(questionCopy.Evaluators).Criteria).Id);
        string copiedQuestionId = questionCopy.Id;
        viewModel.MoveQuestionUp(copiedQuestionId);
        Assert.Equal(copiedQuestionId, viewModel.Questions[0].Id);
        Assert.Equal(copiedQuestionId, viewModel.Draft.Questions[0].Id);
        questionCopy.Enabled = false;
        Assert.False(Assert.Single(viewModel.Draft.Questions, question => question.Id == copiedQuestionId).Enabled);
        Assert.Contains(viewModel.Questions, question => question.Id == copiedQuestionId);

        QuantificationSnapshot disabledQuestionSnapshot = viewModel.BuildSnapshot();
        Assert.False(Assert.Single(
            disabledQuestionSnapshot.Definition.Questions,
            question => question.Id == copiedQuestionId).Enabled);
        viewModel.DeleteQuestion(copiedQuestionId);
        Assert.DoesNotContain(viewModel.Questions, question => question.Id == copiedQuestionId);

        EvaluatorDesignItemViewModel evaluatorCopy = viewModel.DuplicateEvaluator(
            originalQuestion.Id,
            originalEvaluator.Id);
        Assert.NotEqual(originalEvaluator.Id, evaluatorCopy.Id);
        Assert.NotEqual(originalCriterion.Id, Assert.Single(evaluatorCopy.Criteria).Id);
        string copiedEvaluatorId = evaluatorCopy.Id;
        viewModel.MoveEvaluatorUp(originalQuestion.Id, copiedEvaluatorId);
        Assert.Equal(copiedEvaluatorId, originalQuestion.Evaluators[0].Id);
        evaluatorCopy.Enabled = false;
        Assert.False(Assert.Single(
            viewModel.Draft.Questions.Single(question => question.Id == originalQuestion.Id).Evaluators,
            evaluator => evaluator.Id == copiedEvaluatorId).Enabled);
        Assert.True(viewModel.BuildSnapshot().Definition.Questions
            .Single(question => question.Id == originalQuestion.Id).Evaluators
            .Any(evaluator => evaluator.Id == copiedEvaluatorId && !evaluator.Enabled));
        viewModel.DeleteEvaluator(originalQuestion.Id, copiedEvaluatorId);
        Assert.DoesNotContain(originalQuestion.Evaluators, evaluator => evaluator.Id == copiedEvaluatorId);

        EvaluatorDesignItemViewModel custom = viewModel.AddEvaluator(
            originalQuestion.Id,
            EvaluatorType.CustomPrompt);
        Assert.Equal(EvaluatorType.CustomPrompt, custom.Type);
        CriterionDesignItemViewModel criterionCopy = viewModel.DuplicateCriterion(
            originalQuestion.Id,
            custom.Id,
            Assert.Single(custom.Criteria).Id);
        string copiedCriterionId = criterionCopy.Id;
        viewModel.MoveCriterionUp(originalQuestion.Id, custom.Id, copiedCriterionId);
        Assert.Equal(copiedCriterionId, custom.Criteria[0].Id);
        criterionCopy.Enabled = false;
        Assert.False(Assert.Single(
            viewModel.Draft.Questions.Single(question => question.Id == originalQuestion.Id).Evaluators
                .Single(evaluator => evaluator.Id == custom.Id).Criteria,
            criterion => criterion.Id == copiedCriterionId).Enabled);
        Assert.True(viewModel.BuildSnapshot().Definition.Questions
            .Single(question => question.Id == originalQuestion.Id).Evaluators
            .Single(evaluator => evaluator.Id == custom.Id).Criteria
            .Any(criterion => criterion.Id == copiedCriterionId && !criterion.Enabled));
        viewModel.DeleteCriterion(originalQuestion.Id, custom.Id, copiedCriterionId);
        Assert.DoesNotContain(custom.Criteria, criterion => criterion.Id == copiedCriterionId);

        QuestionDesignItemViewModel addedQuestion = viewModel.AddQuestion();
        string addedQuestionId = addedQuestion.Id;
        viewModel.DeleteQuestion(addedQuestionId);
        Assert.DoesNotContain(viewModel.Questions, question => question.Id == addedQuestionId);
        Assert.Equal(originalQuestion.Id, activeSnapshot.Definition.Questions[0].Id);
        Assert.Single(activeSnapshot.Definition.Questions);
        Assert.Single(activeSnapshot.Definition.Questions[0].Evaluators);
        Assert.Single(activeSnapshot.Definition.Questions[0].Evaluators[0].Criteria);
        Assert.True(activeSnapshot.HasValidHash());
        Assert.True(viewModel.IsValid);
    }

    [Fact]
    public void Knowledge_prompt_is_immutable_semantic_coverage_and_evaluator_types_are_closed()
    {
        QuantificationDesignViewModel viewModel = new();
        EvaluatorDesignItemViewModel knowledge = Assert.Single(Assert.Single(viewModel.Questions).Evaluators);
        string originalPreview = knowledge.PromptPreview;

        Assert.Equal(EvaluatorType.KnowledgeCoverage, knowledge.Type);
        Assert.Equal("KNOWLEDGE_COVERAGE", knowledge.TypeContractName);
        Assert.True(knowledge.IsPromptReadOnly);
        Assert.Null(knowledge.CustomPromptTemplate);
        Assert.Contains("説明", knowledge.PromptPreview, StringComparison.Ordinal);
        Assert.Contains("関係", knowledge.PromptPreview, StringComparison.Ordinal);
        Assert.Contains("適用", knowledge.PromptPreview, StringComparison.Ordinal);
        Assert.Contains("単語が存在するだけで満点にせず", knowledge.PromptPreview, StringComparison.Ordinal);
        Assert.Contains("APP-OWNED STRUCTURED OUTPUT CONTRACT", knowledge.PromptPreview, StringComparison.Ordinal);

        knowledge.CustomPromptTemplate = "単語数だけを数える PRIVATE-CANARY";

        Assert.Equal(originalPreview, knowledge.PromptPreview);
        Assert.Null(Assert.Single(viewModel.Draft.Questions).Evaluators[0].CustomPromptTemplate);
        Assert.Equal(
            [EvaluatorType.KnowledgeCoverage, EvaluatorType.CustomPrompt],
            QuantificationDesignViewModel.EvaluatorTypes.Select(choice => choice.Type));
        Assert.Equal(
            ["KNOWLEDGE_COVERAGE", "CUSTOM_PROMPT"],
            QuantificationDesignViewModel.EvaluatorTypes.Select(choice => choice.ContractName));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => viewModel.AddEvaluator(Assert.Single(viewModel.Questions).Id, (EvaluatorType)99));
    }

    [Fact]
    public void Custom_template_validation_handles_required_placeholders_unknown_and_escaped_braces()
    {
        QuantificationDesignViewModel viewModel = new();
        QuestionDesignItemViewModel question = Assert.Single(viewModel.Questions);
        EvaluatorDesignItemViewModel custom = viewModel.AddEvaluator(question.Id, EvaluatorType.CustomPrompt);

        custom.CustomPromptTemplate = string.Empty;
        AssertPromptError(viewModel, custom.Id, "TEMPLATE_REQUIRED");
        Assert.False(viewModel.TryBuildSnapshot(out _));

        custom.CustomPromptTemplate = "{評価項目}";
        AssertPromptError(viewModel, custom.Id, "ANSWER_PLACEHOLDER_REQUIRED");

        custom.CustomPromptTemplate = "{回答}";
        AssertPromptError(viewModel, custom.Id, "CRITERIA_PLACEHOLDER_REQUIRED");

        custom.CustomPromptTemplate = "{回答} {評価項目} {未知}";
        AssertPromptError(viewModel, custom.Id, "UNKNOWN_PLACEHOLDER");

        custom.CustomPromptTemplate = "{回答} {評価項目";
        AssertPromptError(viewModel, custom.Id, "UNCLOSED_PLACEHOLDER");

        custom.CustomPromptTemplate = "literal {{brace}} / answer={回答} / criteria={評価項目}";

        Assert.DoesNotContain(
            viewModel.ValidationErrors,
            error => error.NodeId == custom.Id && error.Field == "CustomPromptTemplate");
        Assert.Contains("literal {brace}", custom.PromptPreview, StringComparison.Ordinal);
        Assert.Contains("【回答 preview】", custom.PromptPreview, StringComparison.Ordinal);
        Assert.Contains("【評価項目 preview】", custom.PromptPreview, StringComparison.Ordinal);
        Assert.True(viewModel.IsValid);
        Assert.True(viewModel.TryBuildSnapshot(out QuantificationSnapshot? snapshot));
        Assert.NotNull(snapshot);

        const string canary = "PRIVATE-PROMPT-CONTENT-CANARY";
        custom.CustomPromptTemplate = canary;
        DesignValidationError error = Assert.Single(
            viewModel.ValidationErrors,
            item => item.NodeId == custom.Id && item.Code == "ANSWER_PLACEHOLDER_REQUIRED");
        Assert.DoesNotContain(canary, error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(canary, error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Three_level_weights_and_optional_ranges_show_effective_values_and_block_invalid_snapshot()
    {
        QuantificationDesignViewModel viewModel = new();
        QuestionDesignItemViewModel firstQuestion = Assert.Single(viewModel.Questions);
        QuestionDesignItemViewModel secondQuestion = viewModel.DuplicateQuestion(firstQuestion.Id);
        firstQuestion.Weight = 2m;
        secondQuestion.Weight = 1m;

        Assert.Equal(200m / 3m, firstQuestion.EffectiveWeightPercentage);
        Assert.Equal(100m / 3m, secondQuestion.EffectiveWeightPercentage);

        EvaluatorDesignItemViewModel firstEvaluator = Assert.Single(firstQuestion.Evaluators);
        EvaluatorDesignItemViewModel secondEvaluator = viewModel.DuplicateEvaluator(
            firstQuestion.Id,
            firstEvaluator.Id);
        firstEvaluator.Weight = 3m;
        secondEvaluator.Weight = 1m;
        Assert.Equal(75m, firstEvaluator.EffectiveWeightPercentage);
        Assert.Equal(25m, secondEvaluator.EffectiveWeightPercentage);
        secondEvaluator.Enabled = false;
        Assert.Equal(100m, firstEvaluator.EffectiveWeightPercentage);
        Assert.Equal(0m, secondEvaluator.EffectiveWeightPercentage);
        secondEvaluator.Enabled = true;

        CriterionDesignItemViewModel firstCriterion = Assert.Single(firstEvaluator.Criteria);
        CriterionDesignItemViewModel secondCriterion = viewModel.DuplicateCriterion(
            firstQuestion.Id,
            firstEvaluator.Id,
            firstCriterion.Id);
        firstCriterion.Weight = 1m;
        secondCriterion.Weight = 3m;
        Assert.Equal(25m, firstCriterion.EffectiveWeightPercentage);
        Assert.Equal(75m, secondCriterion.EffectiveWeightPercentage);
        Assert.False(firstCriterion.HasCustomRange);
        Assert.Equal(new ScoreRange(0m, 10m), firstCriterion.EffectiveRange);

        firstCriterion.HasCustomRange = true;
        firstCriterion.Minimum = 1m;
        firstCriterion.Maximum = 5m;
        Assert.True(firstCriterion.HasCustomRange);
        Assert.Equal(new ScoreRange(1m, 5m), firstCriterion.EffectiveRange);
        Assert.Equal(new ScoreRange(1m, 5m), viewModel.Draft.Questions
            .Single(question => question.Id == firstQuestion.Id).Evaluators
            .Single(evaluator => evaluator.Id == firstEvaluator.Id).Criteria
            .Single(criterion => criterion.Id == firstCriterion.Id).Range);

        firstCriterion.Maximum = 1m;
        Assert.False(viewModel.CanBuildSnapshot);
        Assert.Contains(
            viewModel.ValidationErrors,
            error => error.NodeId == firstCriterion.Id && error.Field == "Range");
        Assert.Throws<QuantificationDesignValidationException>(() => viewModel.BuildSnapshot());
        firstCriterion.Maximum = 5m;
        firstCriterion.HasCustomRange = false;
        Assert.Equal(new ScoreRange(0m, 10m), firstCriterion.EffectiveRange);

        firstEvaluator.Minimum = 10m;
        Assert.False(viewModel.CanBuildSnapshot);
        firstEvaluator.Minimum = 0m;
        firstQuestion.Weight = 0m;
        Assert.False(viewModel.CanBuildSnapshot);
        Assert.Contains(
            viewModel.ValidationErrors,
            error => error.NodeId == firstQuestion.Id && error.Field == "Weight");
        firstQuestion.Weight = 2m;
        Assert.True(viewModel.CanBuildSnapshot);
        Assert.True(viewModel.BuildSnapshot().HasValidHash());
    }

    [Fact]
    public void Extreme_positive_weights_show_effective_percentages_without_decimal_overflow()
    {
        QuantificationDesignViewModel viewModel = new();
        QuestionDesignItemViewModel firstQuestion = Assert.Single(viewModel.Questions);
        firstQuestion.Weight = decimal.MaxValue;
        QuestionDesignItemViewModel secondQuestion = viewModel.DuplicateQuestion(firstQuestion.Id);
        Assert.Equal(50m, firstQuestion.EffectiveWeightPercentage);
        Assert.Equal(50m, secondQuestion.EffectiveWeightPercentage);

        EvaluatorDesignItemViewModel firstEvaluator = Assert.Single(firstQuestion.Evaluators);
        firstEvaluator.Weight = decimal.MaxValue;
        EvaluatorDesignItemViewModel secondEvaluator = viewModel.DuplicateEvaluator(
            firstQuestion.Id,
            firstEvaluator.Id);
        Assert.Equal(50m, firstEvaluator.EffectiveWeightPercentage);
        Assert.Equal(50m, secondEvaluator.EffectiveWeightPercentage);

        CriterionDesignItemViewModel firstCriterion = Assert.Single(firstEvaluator.Criteria);
        firstCriterion.Weight = decimal.MaxValue;
        CriterionDesignItemViewModel secondCriterion = viewModel.DuplicateCriterion(
            firstQuestion.Id,
            firstEvaluator.Id,
            firstCriterion.Id);
        Assert.Equal(50m, firstCriterion.EffectiveWeightPercentage);
        Assert.Equal(50m, secondCriterion.EffectiveWeightPercentage);
        Assert.True(viewModel.CanBuildSnapshot);
    }

    [Fact]
    public void Removing_the_last_enabled_child_is_retained_as_an_invalid_draft_until_fixed()
    {
        QuantificationDesignViewModel viewModel = new();
        QuestionDesignItemViewModel question = Assert.Single(viewModel.Questions);
        EvaluatorDesignItemViewModel evaluator = Assert.Single(question.Evaluators);
        CriterionDesignItemViewModel criterion = Assert.Single(evaluator.Criteria);

        viewModel.DeleteCriterion(question.Id, evaluator.Id, criterion.Id);

        Assert.Empty(evaluator.Criteria);
        Assert.Empty(viewModel.Draft.Questions[0].Evaluators[0].Criteria);
        Assert.False(viewModel.TryBuildSnapshot(out QuantificationSnapshot? snapshot));
        Assert.Null(snapshot);
        Assert.Contains(
            viewModel.ValidationErrors,
            error => error.NodeId == evaluator.Id && error.Code == "ENABLED_CRITERION_REQUIRED");

        viewModel.AddCriterion(question.Id, evaluator.Id);
        Assert.True(viewModel.CanBuildSnapshot);
    }

    [Fact]
    public void Design_contract_has_no_warning_acknowledgement_state()
    {
        string[] prohibitedTerms = ["Warning", "Ethics", "Acknowledge", "Consent", "Dismiss"];
        MemberInfo[] members = typeof(QuantificationDesignViewModel).GetMembers(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        Assert.DoesNotContain(
            members,
            member => prohibitedTerms.Any(term => member.Name.Contains(term, StringComparison.OrdinalIgnoreCase)));
    }

    [AvaloniaFact]
    public void Large_valid_definition_virtualizes_navigators_and_realizes_only_the_selected_editors()
    {
        QuantificationDesignViewModel viewModel = new(CreateLargeDefinition());
        QuantificationDesignView view = new(viewModel);
        Window window = new()
        {
            Width = 1260,
            Height = 900,
            Content = view,
        };

        try
        {
            window.Show();
            window.SetRenderScaling(2d);
            Dispatcher.UIThread.RunJobs();

            Assert.True(viewModel.IsValid);
            Assert.Equal(10, viewModel.Questions.Count);
            Assert.Equal(50, viewModel.Questions.Sum(question => question.Evaluators.Count));
            Assert.Equal(
                1_000,
                viewModel.Questions.Sum(question => question.Evaluators.Sum(evaluator => evaluator.Criteria.Count)));
            Assert.True(viewModel.BuildSnapshot().HasValidHash());
            Assert.True(view.GetVisualDescendants().OfType<VirtualizingStackPanel>().Count() >= 3);
            Assert.InRange(view.GetVisualDescendants().OfType<TextBox>().Count(), 1, 30);
            Assert.Equal(2d, window.RenderScaling);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Standalone_design_view_exposes_read_only_and_editable_prompt_surfaces_with_keyboard_and_two_hundred_percent_scroll()
    {
        QuantificationDesignViewModel viewModel = new();
        QuestionDesignItemViewModel question = Assert.Single(viewModel.Questions);
        EvaluatorDesignItemViewModel knowledge = Assert.Single(question.Evaluators);
        EvaluatorDesignItemViewModel custom = viewModel.AddEvaluator(question.Id, EvaluatorType.CustomPrompt);
        QuantificationDesignView view = new(viewModel);
        Window window = new()
        {
            Width = 1260,
            Height = 900,
            Content = view,
        };

        try
        {
            window.Show();
            window.SetRenderScaling(2d);
            Dispatcher.UIThread.RunJobs();

            TextBox name = Required<TextBox>(view, "DefinitionNameTextBox");
            ScrollViewer scroll = Required<ScrollViewer>(view, "QuantificationDesignScrollViewer");
            ListBox questions = Required<ListBox>(view, "QuestionEditorList");
            Border validation = Required<Border>(view, "DesignValidationSummary");
            TextBox knowledgePreview = Assert.Single(
                view.GetVisualDescendants().OfType<TextBox>(),
                textBox => AutomationProperties.GetAutomationId(textBox) == knowledge.PromptPreviewAutomationId);

            Assert.Same(viewModel, view.DataContext);
            Assert.Equal(2d, window.RenderScaling);
            Assert.Equal(ScrollBarVisibility.Auto, scroll.HorizontalScrollBarVisibility);
            Assert.Equal(ScrollBarVisibility.Auto, scroll.VerticalScrollBarVisibility);
            Assert.Equal("DesignQuestions", AutomationProperties.GetAutomationId(questions));
            Assert.Equal("DesignValidationSummary", AutomationProperties.GetAutomationId(validation));
            Assert.Same(name, window.FocusManager?.GetFocusedElement());
            Assert.True(name.MinHeight >= 44d);
            Assert.True(knowledgePreview.IsVisible);
            Assert.True(knowledgePreview.IsReadOnly);
            Assert.Contains("説明", knowledgePreview.Text ?? string.Empty, StringComparison.Ordinal);
            Assert.Contains(view.GetVisualDescendants(), descendant => descendant is VirtualizingStackPanel);
            Assert.Null(view.FindControl<Border>("EthicsWarningBanner"));

            question.SelectedEvaluator = custom;
            Dispatcher.UIThread.RunJobs();
            TextBox customEditor = Assert.Single(
                view.GetVisualDescendants().OfType<TextBox>(),
                textBox => AutomationProperties.GetAutomationId(textBox) == custom.PromptAutomationId);
            TextBox customPreview = Assert.Single(
                view.GetVisualDescendants().OfType<TextBox>(),
                textBox => AutomationProperties.GetAutomationId(textBox) == custom.PromptPreviewAutomationId);
            Assert.False(customEditor.IsReadOnly);
            Assert.True(customEditor.IsVisible);
            Assert.True(customPreview.IsVisible);
            Assert.True(customPreview.IsReadOnly);

            Press(window, Key.Tab);
            Assert.NotSame(name, window.FocusManager?.GetFocusedElement());
            Assert.True(customEditor.Focus(NavigationMethod.Tab, KeyModifiers.None));
            Assert.Same(customEditor, window.FocusManager?.GetFocusedElement());
            Assert.Equal(new Thickness(3d), customEditor.BorderThickness);
        }
        finally
        {
            window.Close();
        }
    }

    private static void AssertPromptError(
        QuantificationDesignViewModel viewModel,
        string evaluatorId,
        string expectedCode) =>
        Assert.Contains(
            viewModel.ValidationErrors,
            error => error.NodeId == evaluatorId
                && error.Field == "CustomPromptTemplate"
                && error.Code == expectedCode);

    private static QuantificationDefinition CreateLargeDefinition() => new()
    {
        Id = "definition-large",
        Name = "Large virtualized design",
        Revision = "1",
        SourceSheet = "Original",
        HeaderRow = 1,
        FirstDataRow = 2,
        LastDataRow = 531,
        RoundingDigits = 1,
        Questions =
        [
            .. Enumerable.Range(1, 10).Select(question => new QuestionDefinition
            {
                Id = $"question-{question}",
                DisplayName = $"Question {question}",
                QuestionText = $"Question text {question}",
                PrimarySourceColumn = "A",
                SupportingSourceColumns = [],
                Weight = question,
                Enabled = true,
                Evaluators =
                [
                    .. Enumerable.Range(1, 5).Select(evaluator => new EvaluatorDefinition
                    {
                        Id = $"evaluator-{question}-{evaluator}",
                        DisplayName = $"Knowledge {question}-{evaluator}",
                        Type = EvaluatorType.KnowledgeCoverage,
                        Weight = evaluator,
                        Range = new ScoreRange(0m, 10m),
                        BuiltInTemplateVersion = BuiltInPromptTemplates.KnowledgeTemplateVersion,
                        CustomPromptTemplate = null,
                        Enabled = true,
                        Criteria =
                        [
                            .. Enumerable.Range(1, 20).Select(criterion => new CriterionDefinition
                            {
                                Id = $"criterion-{question}-{evaluator}-{criterion}",
                                DisplayName = $"Criterion {question}-{evaluator}-{criterion}",
                                Description = "説明・関係・適用を確認する知識ポイント",
                                Weight = criterion,
                                Range = null,
                                Enabled = true,
                            }),
                        ],
                    }),
                ],
            }),
        ],
    };

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